namespace SoftwareManager.Services;

public class ScanResult
{
    public List<string> RegistryKeys { get; } = new();
    public List<string> Folders { get; } = new();
    public List<string> Files { get; } = new();

    public bool IsEmpty => RegistryKeys.Count == 0 && Folders.Count == 0 && Files.Count == 0;
}
