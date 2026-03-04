using System.Text.Json;

namespace Updater
{
    static class AppHelper
    {
        public static string ServerUrl = "http://192.168.16.52:15000";
        public static string InstallRoot = @"D:\SoftwareManager\apps";
        public static readonly string UpdaterDir = @"D:\SoftwareManager";

        public static string? ParseAppId(string[] args)
        {
            foreach (var arg in args)
                if (arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
                    return arg["--app=".Length..];
            return null;
        }

        public static void LoadConfig()
        {
            var paths = new[]
            {
                Path.Combine(UpdaterDir, "config.json"),
                Path.Combine(AppContext.BaseDirectory, "config.json"),
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p))
                    continue;
                try
                {
                    var cfg = JsonSerializer.Deserialize<UpdaterConfig>(
                        File.ReadAllText(p),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    if (cfg != null)
                    {
                        ServerUrl = cfg.ServerUrl.TrimEnd('/');
                        InstallRoot = cfg.InstallRoot;
                        return;
                    }
                }
                catch { }
            }
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024:F1} MB";
        }
    }
}
