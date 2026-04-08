using System.Diagnostics;
using System.Text.Json;

namespace Bootstrap
{
    static class AppHelper
    {
        public static string ServerUrl = "http://127.0.0.1:15000";
        public static string InstallDir = @"D:\SoftwareLibrary";
        public static string InstallRoot = @"D:\SoftwareLibrary\apps";

        public static string UpdaterPath => Path.Combine(InstallDir, "Updater.exe");
        public static string ConfigPath => Path.Combine(InstallDir, "config.json");

        public static void LoadConfig()
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config.json"),
                Path.Combine(InstallDir, "config.json"),
            };

            foreach (var jsonPath in paths)
            {
                if (!File.Exists(jsonPath))
                    continue;
                try
                {
                    var cfg = JsonSerializer.Deserialize<BootstrapConfig>(
                        File.ReadAllText(jsonPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    if (cfg != null)
                    {
                        ServerUrl = cfg.ServerUrl.TrimEnd('/');
                        if (!string.IsNullOrWhiteSpace(cfg.InstallRoot))
                            InstallRoot = cfg.InstallRoot;
                        return;
                    }
                }
                catch { }
            }
        }

        public static void WriteConfig()
        {
            var config = new { serverUrl = ServerUrl, installRoot = InstallRoot };
            File.WriteAllText(
                ConfigPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })
            );
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

        public static string GetLocalUpdaterVersion()
        {
            var f = Path.Combine(InstallDir, "updater-version.txt");
            return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
        }

        public static void SaveLocalUpdaterVersion(string version)
        {
            File.WriteAllText(Path.Combine(InstallDir, "updater-version.txt"), version);
        }
    }

    class BootstrapConfig
    {
        public string ServerUrl { get; set; } = "http://127.0.0.1:15000";
        public string InstallRoot { get; set; } = @"D:\SoftwareLibrary\apps";
    }
}
