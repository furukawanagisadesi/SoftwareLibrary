using System.Diagnostics;
using System.Text.Json;

namespace Bootstrap
{
    static class AppHelper
    {
        public static string ServerUrl = "http://127.0.0.1:15000";
        public static string InstallRoot = @"D:\SoftwareLibrary\apps";

        // 从 InstallRoot 推导，不单独存字段
        public static string InstallDir => Path.GetDirectoryName(InstallRoot)!;
        public static string ConfigPath => Path.Combine(InstallDir, "config.json");
        public static string UpdaterPath => Path.Combine(InstallRoot, "updater", "Updater.exe");
        public static string InstalledRecordPath => Path.Combine(InstallDir, "installed.json");

        // 返回实际使用的配置路径，供调用方显示提示
        public static string? LoadConfig()
        {
            var localConfigPath = Path.Combine(AppContext.BaseDirectory, "config.json");

            // 第一步：读当前目录的 config.json，拿到 installRoot
            BootstrapConfig? localCfg = null;
            if (File.Exists(localConfigPath))
            {
                try
                {
                    localCfg = JsonSerializer.Deserialize<BootstrapConfig>(
                        File.ReadAllText(localConfigPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                }
                catch { }
            }

            if (localCfg != null)
            {
                ServerUrl = localCfg.ServerUrl.TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(localCfg.InstallRoot))
                    InstallRoot = localCfg.InstallRoot;
            }

            // 第二步：推导 configPath，检查是否已初始化
            var configPath = ConfigPath;
            if (File.Exists(configPath))
            {
                try
                {
                    var installedCfg = JsonSerializer.Deserialize<BootstrapConfig>(
                        File.ReadAllText(configPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    // 已初始化，以 configPath 的值为准
                    if (installedCfg?.FirstInitialized == true)
                    {
                        var prevServerUrl = ServerUrl;
                        var prevInstallRoot = InstallRoot;

                        ServerUrl = installedCfg.ServerUrl.TrimEnd('/');
                        if (!string.IsNullOrWhiteSpace(installedCfg.InstallRoot))
                            InstallRoot = installedCfg.InstallRoot;

                        // 若两份配置不一致，返回路径供调用方提示
                        if (
                            prevServerUrl != ServerUrl
                            || prevInstallRoot != InstallRoot
                        )
                            return configPath;

                        return null; // 一致，无需提示
                    }
                }
                catch { }
            }

            // 第三步：首次运行，写入 configPath
            WriteConfig(firstInit: true);
            return configPath; // 首次初始化也提示一次
        }

        // firstInit=true 时写入 firstInitialized:true，否则保留原值
        public static void WriteConfig(bool firstInit = false)
        {
            var config = new BootstrapConfig
            {
                ServerUrl = ServerUrl,
                InstallRoot = InstallRoot,
                FirstInitialized = firstInit || GetFirstInitialized(),
            };
            File.WriteAllText(
                ConfigPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
            );
        }

        // 读取现有 config.json 里的 firstInitialized，避免重写时丢失
        private static bool GetFirstInitialized()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return false;
                var cfg = JsonSerializer.Deserialize<BootstrapConfig>(
                    File.ReadAllText(ConfigPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                return cfg?.FirstInitialized ?? false;
            }
            catch { return false; }
        }

        public static void LaunchUpdater(string args)
        {
            Process.Start(
                new ProcessStartInfo(UpdaterPath)
                {
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = InstallDir,
                }
            );
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
    }

    class BootstrapConfig
    {
        public string ServerUrl { get; set; } = "http://127.0.0.1:15000";
        public string InstallRoot { get; set; } = @"D:\SoftwareLibrary\apps";
        public bool FirstInitialized { get; set; } = false;
    }

    record InstalledRecord
    {
        public string Id { get; init; } = "";
        public string Version { get; init; } = "";
        public string InstallPath { get; init; } = "";
        public DateTime InstalledAt { get; init; }
    }
}
