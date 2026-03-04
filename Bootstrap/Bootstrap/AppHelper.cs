using System.Diagnostics;
using System.Text.Json;

namespace Bootstrap
{
    static class AppHelper
    {
        public static readonly string InstallDir = @"D:\SoftwareManager";
        public static readonly string UpdaterPath = Path.Combine(InstallDir, "Updater.exe");
        public static readonly string ConfigPath = Path.Combine(InstallDir, "config.json");
        public static string ServerUrl = "http://192.168.16.52:15000";

        public static void LoadConfig()
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config.ini"),
                Path.Combine(InstallDir, "config.ini"),
            };

            foreach (var iniPath in paths)
            {
                if (!File.Exists(iniPath))
                    continue;
                foreach (var line in File.ReadAllLines(iniPath))
                {
                    var parts = line.Split('=', 2);
                    if (
                        parts.Length == 2
                        && parts[0].Trim().Equals("ServerUrl", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        ServerUrl = parts[1].Trim().TrimEnd('/');
                        return;
                    }
                }
            }
        }

        public static void WriteConfig()
        {
            if (File.Exists(ConfigPath))
                return;
            var config = new
            {
                serverUrl = ServerUrl,
                installRoot = Path.Combine(InstallDir, "apps"),
            };
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
}
