using System.Text.Json;
using SoftwareManager.Models;

namespace SoftwareManager.Services;

public abstract class ServiceBase
{
    protected readonly AppConfig _config;
    protected List<SoftwarePackage>? _serverList;

    // 系统组件：显示在列表里但禁止卸载
    public static readonly HashSet<string> SystemIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "bootstrap",
        "updater",
        "softwaremanager",
    };

    protected ServiceBase(AppConfig config)
    {
        _config = config;
    }

    // 缓存服务器列表，供 Uninstall 查 ExeName
    public void CacheServerList(List<SoftwarePackage> list) => _serverList = list;

    // 读取本地已安装记录
    public List<InstalledRecord> GetInstalledRecords()
    {
        if (!File.Exists(_config.InstalledRecordPath))
            return [];

        try
        {
            var json = File.ReadAllText(_config.InstalledRecordPath);
            var records =
                JsonSerializer.Deserialize<List<InstalledRecord>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? [];

            // 过滤掉安装未完成的（有 .installing 标记的）
            return records
                .Where(r =>
                {
                    var mark = Path.Combine(r.InstallPath, ".installing");
                    return !File.Exists(mark);
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    protected void SaveRecord(InstalledRecord record)
    {
        var records = new List<InstalledRecord>();
        if (File.Exists(_config.InstalledRecordPath))
        {
            try
            {
                var json = File.ReadAllText(_config.InstalledRecordPath);
                records =
                    JsonSerializer.Deserialize<List<InstalledRecord>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? [];
                records.RemoveAll(r => string.IsNullOrEmpty(r.Id));
            }
            catch { }
        }

        records.RemoveAll(r => r.Id == record.Id);
        records.Add(record);
        SaveRecords(records);
    }

    protected void SaveRecords(List<InstalledRecord> records)
    {
        var json = JsonSerializer.Serialize(
            records,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(_config.InstalledRecordPath, json);
    }
}
