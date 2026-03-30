using System.Diagnostics;
using System.Text.Json;

namespace Bootstrap
{
    static class AppHelper
    {
        public static string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoftwareLibrary"
        );
        public static readonly string UpdaterPath = Path.Combine(InstallDir, "Updater.exe");
        public static readonly string ConfigPath = Path.Combine(InstallDir, "config.json");
        public static string ServerUrl = "http://127.0.0.1:15000";

        public static void LoadConfig()
        {
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "config.json"),
                ConfigPath, // D:\SoftwareLibrary\config.json
            };

            foreach (var jsonPath in paths)
            {
                if (!File.Exists(jsonPath))
                    continue;
                try
                {
                    var text = File.ReadAllText(jsonPath);
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("serverUrl", out var el))
                    {
                        var val = el.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            ServerUrl = val.TrimEnd('/');
                            return;
                        }
                    }
                }
                catch { }
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
