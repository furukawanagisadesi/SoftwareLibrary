using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SoftwareServer.Models;

namespace SoftwareServer.Services;

public class SoftwareService
{
    private readonly IConfiguration _config;
    private readonly string _packagesDir;
    private readonly string _metaFile;
    private readonly string _serverUrl;
    private readonly bool _autoCommitScoop;
    private readonly ILogger<SoftwareService> _logger;

    public SoftwareService(IConfiguration config, ILogger<SoftwareService> logger)
    {
        _config = config;
        _logger = logger;
        _packagesDir =
            config["Storage:PackagesDir"] ?? Path.Combine(AppContext.BaseDirectory, "packages");
        _serverUrl = (config["ServerUrl"] ?? "http://localhost:15000").TrimEnd('/');
        _autoCommitScoop = config.GetValue("Scoop:AutoCommit", true);
        _metaFile = Path.Combine(_packagesDir, "software-list.json");
        Directory.CreateDirectory(_packagesDir);
    }

    /// <summary>校验软件 ID（仅允许字母数字与 . _ -，防止路径穿越）</summary>
    public static bool IsValidId(string id) =>
        !string.IsNullOrWhiteSpace(id)
        && System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9._-]*$");

    /// <summary>校验版本号（仅允许字母数字与 . _ -，防止路径穿越）</summary>
    public static bool IsValidVersion(string version) =>
        !string.IsNullOrWhiteSpace(version)
        && System.Text.RegularExpressions.Regex.IsMatch(version, @"^[A-Za-z0-9._-]+$");

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
            if (!IsValidId(id))
                throw new ArgumentException("软件 ID 只能包含字母、数字、点、下划线、短横线");
            if (!IsValidVersion(req.Version))
                throw new ArgumentException("版本号包含非法字符");

            // ★ 改为目录结构: packages/{id}/{version}/{id}.zip
            var appDir = Path.Combine(_packagesDir, id, req.Version);
            Directory.CreateDirectory(appDir);

            var zipFileName = $"{id}.zip";  // 压缩包名称就是软件名称
            var zipPath = Path.Combine(appDir, zipFileName);
            await using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await zipFile.CopyToAsync(stream);
            }

            // 校验 zip 完整性：不是合法 zip / 空包则拒绝并清理
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                if (archive.Entries.Count == 0)
                    throw new InvalidDataException("压缩包内没有文件");
            }
            catch
            {
                if (Directory.Exists(appDir))
                    Directory.Delete(appDir, recursive: true);
                throw;
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

            // 自动提交到 scoop bucket git 仓库（使远端立即生效）
            CommitScoopBucketIfChanged($"publish {id} v{req.Version}");

            _logger.LogInformation("发布软件 {Id} 版本 {Version}", id, req.Version);
            return pkg;
        }
        finally
        {
            _lock.Release();
        }
    }

    // 仅更新软件信息，不替换 zip 包；支持修改版本号（自动迁移版本目录）
    // 返回 null 表示成功，否则返回错误消息
    public async Task<string?> UpdateInfo(string id, PublishRequest req)
    {
        if (!IsValidId(id))
            return $"软件 ID 包含非法字符: {id}";

        await _lock.WaitAsync();
        try
        {
            var list = GetAll();
            var pkg = list.FirstOrDefault(s => s.Id == id);
            if (pkg == null)
                return $"软件 {id} 不存在";

            // 版本号变更：迁移目录 packages/{id}/{旧版本}/ → packages/{id}/{新版本}/
            var versionChanged =
                !string.IsNullOrWhiteSpace(req.Version) && req.Version != pkg.Version;

            if (versionChanged)
            {
                // 版本号只能包含安全字符，防止路径穿越
                if (!System.Text.RegularExpressions.Regex.IsMatch(req.Version, @"^[A-Za-z0-9._-]+$"))
                    return $"版本号包含非法字符: {req.Version}";

                var oldDir = Path.Combine(_packagesDir, id, pkg.Version);
                var newDir = Path.Combine(_packagesDir, id, req.Version);

                // 目标目录已存在且非空 → 拒绝，避免覆盖已有包
                if (Directory.Exists(newDir) && Directory.EnumerateFileSystemEntries(newDir).Any())
                    return $"版本目录已存在且非空: {req.Version}，请先删除该版本或换用其他版本号";

                if (Directory.Exists(oldDir))
                {
                    // 清理可能残留的空目标目录后迁移
                    if (Directory.Exists(newDir))
                        Directory.Delete(newDir, recursive: true);
                    Directory.CreateDirectory(Path.Combine(_packagesDir, id));
                    Directory.Move(oldDir, newDir);
                    _logger.LogInformation(
                        "迁移版本目录 {Old} → {New}",
                        oldDir, newDir);
                }
                // 旧目录不存在时（仅元信息、无包），直接改版本号即可

                pkg.Version = req.Version;
            }

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

            // 自动提交（版本号变更 / 信息变更均触发）
            CommitScoopBucketIfChanged($"update {id} v{pkg.Version}");

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    // 删除软件
    public bool Delete(string id)
    {
        if (!IsValidId(id))
            throw new ArgumentException("软件 ID 包含非法字符");

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

        // 自动提交（manifest 删除后使远端立即生效）
        CommitScoopBucketIfChanged($"delete {id}");

        return true;
    }

    /// <summary>
    /// 重新生成全部 Scoop Manifest。
    /// 用途：ServerUrl 变更（如从 localhost 改为局域网 IP）后，
    /// 批量重建所有 manifest，使新地址立即生效，无需逐个重新发布。
    /// </summary>
    public async Task<int> RegenerateAllManifestsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var list = GetAll();
            foreach (var pkg in list)
                await GenerateScoopManifestAsync(pkg);
            _logger.LogInformation("已重新生成 {Count} 个 Scoop Manifest", list.Count);

            // 自动提交（ServerUrl 等变更后的批量重建）
            CommitScoopBucketIfChanged($"regenerate {list.Count} manifests");

            return list.Count;
        }
        finally
        {
            _lock.Release();
        }
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
    //  Scoop Bucket 自动 git commit
    // ════════════════════════════════════════════

    /// <summary>
    /// 检查 packages/scoop 仓库的 bucket 目录是否有变更，若有则自动 git commit。
    /// 目的：上传/更新/删除 manifest 后自动提交，使 git daemon 提供的
    /// 远端 scoop bucket 立即生效，无需手动运行发布脚本。
    /// 若 Scoop:AutoCommit=false 则跳过。
    /// </summary>
    private void CommitScoopBucketIfChanged(string message)
    {
        if (!_autoCommitScoop)
            return;

        var gitDir = Path.Combine(_packagesDir, "scoop");
        // 优先使用配置 GitDaemon:GitPath（服务场景下 PATH 可能不含 scoop/git）
        var gitPath = _config["GitDaemon:GitPath"];
        if (string.IsNullOrWhiteSpace(gitPath) || !File.Exists(gitPath))
            gitPath = GitDaemonService.FindGitPath();
        if (string.IsNullOrEmpty(gitPath))
        {
            _logger.LogWarning("自动提交 scoop bucket 失败：未找到 git 可执行文件");
            return;
        }

        try
        {
            // 1. 检查是否有变更（含未跟踪的新 manifest）
            var changed = RunGit(gitPath, gitDir, "status", "--porcelain", "bucket/");
            if (string.IsNullOrWhiteSpace(changed))
                return; // 无变更，跳过提交

            // 2. 暂存 bucket 目录（只提交 manifest，不碰其他文件）
            RunGit(gitPath, gitDir, "add", "bucket/");

            // 3. 确保仓库有提交身份（服务/LocalSystem 场景可能无全局 user.name/email）
            EnsureGitIdentity(gitPath, gitDir);

            // 4. 提交
            var result = RunGit(gitPath, gitDir, "commit", "-m", message);
            _logger.LogInformation("自动提交 scoop bucket: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动提交 scoop bucket 失败");
        }
    }

    /// <summary>在指定目录执行 git 命令，返回 stdout；非零退出码抛异常</summary>
    private static string RunGit(string gitPath, string workDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = gitPath,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 git 进程");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(10_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)} 失败: {stderr}");
        return stdout;
    }

    /// <summary>读取本地 git 配置；key 不存在时返回 null（git config --get 退出码为 1）</summary>
    private static string? GetGitConfig(string gitPath, string workDir, string key)
    {
        try
        {
            var value = RunGit(gitPath, workDir, "config", "--get", key);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>仓库缺失 user.name / user.email 时写入本地配置，保证自动 commit 可用</summary>
    private static void EnsureGitIdentity(string gitPath, string workDir)
    {
        if (GetGitConfig(gitPath, workDir, "user.name") == null)
            RunGit(gitPath, workDir, "config", "user.name", "SoftwareServer");
        if (GetGitConfig(gitPath, workDir, "user.email") == null)
            RunGit(gitPath, workDir, "config", "user.email", "softwareserver@localhost");
    }

    // ════════════════════════════════════════════
    //  Scoop Manifest 生成
    // ════════════════════════════════════════════

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
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
        var sha256 = await ComputeSha256Async(zipPath);

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
