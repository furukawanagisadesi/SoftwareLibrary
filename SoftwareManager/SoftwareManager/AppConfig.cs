using System.Text.Json;

namespace SoftwareManager;

public class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory,
        "config.json"
    );

    public string ServerUrl { get; set; } = "http://192.168.16.52:15000";
    public string InstallRoot { get; set; } = @"D:\SoftwareManager\apps";

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var def = new AppConfig();
            def.Save();
            return def;
        }
        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(ConfigPath, json);
    }
}
