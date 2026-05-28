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
        public string ServerUrl { get; init; } = "http://127.0.0.1:15000";
        public string InstallRoot { get; init; } = @"D:\SoftwareLibrary\apps";
        public bool FirstInitialized { get; init; } = false;
    }

    record InstalledRecord
    {
        public string Id { get; init; } = "";
        public string Version { get; init; } = "";
        public string InstallPath { get; init; } = "";
        public DateTime InstalledAt { get; init; }
    }
}
