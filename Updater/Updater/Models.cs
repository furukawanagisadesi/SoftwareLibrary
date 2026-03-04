namespace Updater
{
    record SoftwareInfo
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
        public string ExeName { get; init; } = "";
        public long FileSize { get; init; }
    }

    record UpdaterConfig
    {
        public string ServerUrl { get; init; } = "http://192.168.16.52:15000";
        public string InstallRoot { get; init; } = @"D:\SoftwareManager\apps";
    }

    record InstalledRecord
    {
        public string Id { get; init; } = "";
        public string Version { get; init; } = "";
        public string InstallPath { get; init; } = "";
        public DateTime InstalledAt { get; init; }
    }
}
