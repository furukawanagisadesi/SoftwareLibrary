using System.Text.Json;
using System.Text.Json.Serialization;

public class AppConfig
{
    public string ServerUrl { get; set; } = "http://127.0.0.1:15000";
    public string InstallRoot { get; set; } = @"D:\SoftwareLibrary\apps";
    public bool FirstInitialized { get; set; } = false;

    // 基于 InstallRoot 计算，不存 JSON
    [JsonIgnore] // ← 防止序列化这些派生属性
    public string InstallDir => Path.GetDirectoryName(InstallRoot)!;

    [JsonIgnore]
    public string BootstrapPath => Path.Combine(InstallDir!, "Bootstrap.exe");

    [JsonIgnore]
    public string InstalledRecordPath => Path.Combine(InstallDir!, "installed.json");

    // config.json 固定在 InstallDir 下
    [JsonIgnore]
    private string ConfigPath => Path.Combine(InstallDir!, "config.json");

    public static AppConfig Load()
    {
        // 先用默认值创建实例，计算配置路径
        var defaultInstance = new AppConfig();
        var configPath = defaultInstance.ConfigPath;

        if (!File.Exists(configPath))
            return defaultInstance;

        try
        {
            var json = File.ReadAllText(configPath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            return loaded ?? defaultInstance;
        }
        catch
        {
            return defaultInstance;
        }
    }

    public void Save()
    {
        // 只序列化三个核心字段
        var json = JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                // 忽略只读属性和标记 [JsonIgnore] 的属性
            }
        );
        File.WriteAllText(ConfigPath, json);
    }
}
