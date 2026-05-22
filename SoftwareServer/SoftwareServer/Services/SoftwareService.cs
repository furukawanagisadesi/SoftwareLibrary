using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoftwareServer.Models;

namespace SoftwareServer.Services;

public class SoftwareService
{
    private readonly string _packagesDir;
    private readonly string _metaFile;
    private readonly string _serverUrl;
    private readonly ILogger<SoftwareService> _logger;

    public SoftwareService(IConfiguration config, ILogger<SoftwareService> logger)
    {
        _logger = logger;
        _packagesDir =
            config["Storage:PackagesDir"] ?? Path.Combine(AppContext.BaseDirectory, "packages");
        _serverUrl = (config["ServerUrl"] ?? "http://localhost:15000").TrimEnd('/');
        _metaFile = Path.Combine(_packagesDir, "software-list.json");
        Directory.CreateDirectory(_packagesDir);
    }

    // 读取所有软件清单
    public List<SoftwarePackage> GetAll()
    {
        if (!File.Exists(_metaFile))
            return [];
        var json = File.ReadAllText(_metaFile);
        return JsonSerializer.Deserialize<List<SoftwarePackage>>(json) ?? [];
    }

    // 读取单个软件信息
    public SoftwarePackage? GetById(string id)
    {
        return GetAll().FirstOrDefault(s => s.Id == id);
    }

    // 获取 zip 文件路径
    public string? GetZipPath(string id)
    {
        var pkg = GetById(id);
        if (pkg == null)
            return null;
        // ★ 改为目录结构: packages/{id}/{version}/{id}.zip
        var path = Path.Combine(_packagesDir, pkg.Id, pkg.Version, pkg.ZipFileName);
        return File.Exists(path) ? path : null;
    }

    // 上传新软件包并发布版本
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<SoftwarePackage> PublishAsync(
        string id,
        IFormFile zipFile,
        PublishRequest req
    )
    {
        await _lock.WaitAsync();
        try
        {
            // ★ 改为目录结构: packages/{id}/{version}/{id}.zip
            var appDir = Path.Combine(_packagesDir, id, req.Version);
            Directory.CreateDirectory(appDir);

            var zipFileName = $"{id}.zip";  // 压缩包名称就是软件名称
            var zipPath = Path.Combine(appDir, zipFileName);
            await using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(stream);
            }

            // 更新清单
            var list = GetAll();
            var existing = list.FirstOrDefault(s => s.Id == id);

            // ★ 删除旧版本的目录
            if (existing != null && existing.Version != req.Version)
            {
                var oldDir = Path.Combine(_packagesDir, id, existing.Version);
                if (Directory.Exists(oldDir))
                    Directory.Delete(oldDir, recursive: true);
            }

            var pkg = existing ?? new SoftwarePackage { Id = id };
            pkg.Name = req.Name.Length > 0 ? req.Name : pkg.Name;
            pkg.Version = req.Version;
            if (!string.IsNullOrWhiteSpace(req.Description))
                pkg.Description = req.Description;
            pkg.ExeName = req.ExeName;
            pkg.ZipFileName = zipFileName;  // 为 "{id}.zip"
            pkg.FileSize = new FileInfo(zipPath).Length;
            pkg.UpdatedAt = DateTime.Now;

            // Scoop 可选信息
            if (!string.IsNullOrWhiteSpace(req.Homepage))
                pkg.Homepage = req.Homepage;
            if (!string.IsNullOrWhiteSpace(req.License))
                pkg.License = req.License;
            if (!string.IsNullOrWhiteSpace(req.Persist))
                pkg.Persist = [.. req.Persist.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

            if (existing == null)
                list.Add(pkg);

            SaveList(list);

            // ★ 生成 Scoop Manifest
            await GenerateScoopManifestAsync(pkg);

            _logger.LogInformation("发布软件 {Id} 版本 {Version}", id, req.Version);
            return pkg;
        }
        finally
        {
            _lock.Release();
        }
    }

    // 仅更新软件信息，不替换 zip 包
    public async Task<bool> UpdateInfo(string id, PublishRequest req)
    {
        var list = GetAll();
        var pkg = list.FirstOrDefault(s => s.Id == id);
        if (pkg == null)
            return false;

        // ★ 版本号只能通过重新发布（Publish）变更，因为目录名包含版本号
        if (!string.IsNullOrWhiteSpace(req.Version) && req.Version != pkg.Version)
            return false;

        if (!string.IsNullOrWhiteSpace(req.Name))
            pkg.Name = req.Name;
        if (!string.IsNullOrWhiteSpace(req.ExeName))
            pkg.ExeName = req.ExeName;
        if (req.Description != null)
            pkg.Description = req.Description;

        // Scoop 字段更新
        if (req.Homepage != null)
            pkg.Homepage = req.Homepage;
        if (req.License != null)
            pkg.License = req.License;
        if (req.Persist != null)
            pkg.Persist = [.. req.Persist.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

        pkg.UpdatedAt = DateTime.Now;

        SaveList(list);

        // ★ 重新生成 Manifest
        await GenerateScoopManifestAsync(pkg);

        return true;
    }

    // 删除软件
    public bool Delete(string id)
    {
        var list = GetAll();
        var pkg = list.FirstOrDefault(s => s.Id == id);
        if (pkg == null)
            return false;

        // ★ 删除整个软件目录
        var appDir = Path.Combine(_packagesDir, id);
        if (Directory.Exists(appDir))
            Directory.Delete(appDir, recursive: true);

        // ★ 删除 Scoop Manifest
        DeleteScoopManifest(id);

        list.Remove(pkg);
        SaveList(list);
        return true;
    }

    private void SaveList(List<SoftwarePackage> list)
    {
        var json = JsonSerializer.Serialize(
            list,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(_metaFile, json);
    }

    // ════════════════════════════════════════════
    //  Scoop Manifest 生成
    // ════════════════════════════════════════════

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string GetScoopBucketDir()
    {
        return Path.Combine(_packagesDir, "scoop", "bucket");
    }

    private async Task GenerateScoopManifestAsync(SoftwarePackage pkg)
    {
        var bucketDir = GetScoopBucketDir();
        Directory.CreateDirectory(bucketDir);

        var zipPath = Path.Combine(_packagesDir, pkg.Id, pkg.Version, pkg.ZipFileName);
        var sha256 = ComputeSha256(zipPath);

        // 构造 shortcuts: 只有 ExeName 不为空才生成
        object? shortcuts = null;
        if (!string.IsNullOrWhiteSpace(pkg.ExeName))
        {
            shortcuts = new[] { new[] { pkg.ExeName, pkg.Name } };
        }

        var manifest = new Dictionary<string, object?>
        {
            ["version"] = pkg.Version,
            ["description"] = pkg.Name,
            ["url"] = $"{_serverUrl}/api/software/{pkg.Id}/download#{pkg.Id}.zip",
            ["hash"] = $"sha256:{sha256}",
            ["bin"] = string.IsNullOrWhiteSpace(pkg.ExeName) ? null : pkg.ExeName,
            ["shortcuts"] = shortcuts,
        };

        // 可选字段
        if (!string.IsNullOrWhiteSpace(pkg.Homepage))
            manifest["homepage"] = pkg.Homepage;
        if (!string.IsNullOrWhiteSpace(pkg.License))
            manifest["license"] = pkg.License;
        if (pkg.Persist is { Count: > 0 })
            manifest["persist"] = pkg.Persist;

        var json = JsonSerializer.Serialize(
            manifest,
            new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }
        );
        var manifestPath = Path.Combine(bucketDir, $"{pkg.Id}.json");
        await File.WriteAllTextAsync(manifestPath, json);
    }

    private void DeleteScoopManifest(string id)
    {
        var path = Path.Combine(GetScoopBucketDir(), $"{id}.json");
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>ScoopController 使用，获取 Manifest 文件物理路径</summary>
    public string? GetScoopManifestPath(string id)
    {
        var path = Path.Combine(GetScoopBucketDir(), $"{id}.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>获取所有可用 App 的简要列表（供 ScoopController 使用）</summary>
    public List<ScoopAppInfo> GetScoopApps()
    {
        return GetAll().Select(p => new ScoopAppInfo
        {
            Name = p.Id,
            Version = p.Version,
            Description = p.Description ?? p.Name,
        }).ToList();
    }
}

/// <summary>Scoop 应用列表条目</summary>
public class ScoopAppInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
}
