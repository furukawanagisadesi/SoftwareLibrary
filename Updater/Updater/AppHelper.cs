using System.Text.Json;

namespace Updater
{
    static class AppHelper
    {
        public static string ServerUrl = "http://127.0.0.1:15000";
        public static string InstallRoot = @"D:\SoftwareLibrary\apps";

        // 从 InstallRoot 推导，不单独存字段
        public static string InstallDir => Path.GetDirectoryName(InstallRoot)!;
        public static string InstalledRecordPath => Path.Combine(InstallDir, "installed.json");
        public static string BootstrapPath => Path.Combine(InstallDir, "Bootstrap.exe");

        public static string? ParseAppId(string[] args)
        {
            foreach (var arg in args)
                if (arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
                    return arg["--app=".Length..];
            return null;
        }

        // Updater 在 apps\updater\ 下，往上两级得到 InstallDir（D:\SoftwareLibrary\）
        // 不依赖 WorkingDirectory，路径计算确定可靠
        public static void LoadConfig()
        {
            var updaterDir = AppContext.BaseDirectory;                      // apps\updater\
            var appsDir = Path.GetDirectoryName(updaterDir)!;              // apps\
            var installDir = Path.GetDirectoryName(appsDir)!;              // D:\SoftwareLibrary\
            var configPath = Path.Combine(installDir, "config.json");
            if (!File.Exists(configPath))
                return;
            try
            {
                var cfg = JsonSerializer.Deserialize<UpdaterConfig>(
                    File.ReadAllText(configPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (cfg == null)
                    return;
                ServerUrl = cfg.ServerUrl.TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(cfg.InstallRoot))
                    InstallRoot = cfg.InstallRoot;
            }
            catch { }
        }

        // 从 installed.json 读取指定组件的版本
        public static string GetLocalVersion(string id)
        {
            try
            {
                if (!File.Exists(InstalledRecordPath))
                    return "";
                var records = JsonSerializer.Deserialize<List<InstalledRecord>>(
                    File.ReadAllText(InstalledRecordPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                return records?.FirstOrDefault(r => r.Id == id)?.Version ?? "";
            }
            catch { return ""; }
        }

        // 写入或更新 installed.json 里的一条记录
        public static void SaveInstalledRecord(InstalledRecord record)
        {
            var records = new List<InstalledRecord>();
            try
            {
                if (File.Exists(InstalledRecordPath))
                {
                    records = JsonSerializer.Deserialize<List<InstalledRecord>>(
                        File.ReadAllText(InstalledRecordPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? [];
                    records.RemoveAll(r => string.IsNullOrEmpty(r.Id));
                }
            }
            catch { }

            records.RemoveAll(r => r.Id == record.Id);
            records.Add(record);
            File.WriteAllText(
                InstalledRecordPath,
                JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true })
            );
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024:F1} MB";
        }
    }
}
