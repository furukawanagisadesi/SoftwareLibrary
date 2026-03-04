namespace Bootstrap
{
    record SoftwareInfo
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
        public string ExeName { get; init; } = "";
        public long FileSize { get; init; }
    }
}
