using System.Text.Json;
using SoftwareServer.Models;

namespace SoftwareServer.Services;

public class SoftwareService
{
    private readonly string _packagesDir;
    private readonly string _metaFile;
    private readonly ILogger<SoftwareService> _logger;

    public SoftwareService(IConfiguration config, ILogger<SoftwareService> logger)
    {
        _logger = logger;
        _packagesDir =
            config["Storage:PackagesDir"] ?? Path.Combine(AppContext.BaseDirectory, "packages");
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
        var path = Path.Combine(_packagesDir, pkg.ZipFileName);
        return File.Exists(path) ? path : null;
    }

    // 上传新软件包并发布版本
    public async Task<SoftwarePackage> PublishAsync(
        string id,
        IFormFile zipFile,
        PublishRequest req
    )
    {
        // 保存 zip 文件
        var zipFileName = $"{id}_{req.Version}.zip";
        var zipPath = Path.Combine(_packagesDir, zipFileName);
        await using var stream = new FileStream(zipPath, FileMode.Create);
        await zipFile.CopyToAsync(stream);

        // 更新清单
        var list = GetAll();
        var existing = list.FirstOrDefault(s => s.Id == id);

        // 删除旧 zip（如果换了文件名）
        if (existing != null && existing.ZipFileName != zipFileName)
        {
            var oldPath = Path.Combine(_packagesDir, existing.ZipFileName);
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }

        var pkg = existing ?? new SoftwarePackage { Id = id };
        pkg.Name = req.Name.Length > 0 ? req.Name : pkg.Name;
        pkg.Version = req.Version;
        // 如果描述不为空才更新
        if (!string.IsNullOrWhiteSpace(req.Description))
            pkg.Description = req.Description;
        pkg.ExeName = req.ExeName;
        pkg.ZipFileName = zipFileName;
        pkg.FileSize = new FileInfo(zipPath).Length;
        pkg.UpdatedAt = DateTime.Now;

        if (existing == null)
            list.Add(pkg);

        SaveList(list);
        _logger.LogInformation("发布软件 {Id} 版本 {Version}", id, req.Version);
        return pkg;
    }

    // 删除软件
    public bool Delete(string id)
    {
        var list = GetAll();
        var pkg = list.FirstOrDefault(s => s.Id == id);
        if (pkg == null)
            return false;

        var zipPath = Path.Combine(_packagesDir, pkg.ZipFileName);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

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
}
