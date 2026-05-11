using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SoftwareManager.Services;

[SupportedOSPlatform("windows")]
public class AssociationScanner
{
    private readonly string _softwareName;
    private readonly string _exePath;
    private readonly string _exeDir;

    private static readonly string[] UserDataDirs =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
    };

    private static readonly (RegistryHive Hive, string Root, string Display)[] RegistryRoots =
    {
        (RegistryHive.CurrentUser, @"Software", @"HKEY_CURRENT_USER\Software"),
        (RegistryHive.LocalMachine, @"Software", @"HKEY_LOCAL_MACHINE\Software"),
        (
            RegistryHive.LocalMachine,
            @"Software\WOW6432Node",
            @"HKEY_LOCAL_MACHINE\Software\WOW6432Node"
        ),
        (RegistryHive.CurrentUser, @"Software\Classes", @"HKEY_CURRENT_USER\Software\Classes"),
        (
            RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        ),
        (
            RegistryHive.CurrentUser,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
        ),
        (
            RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Services",
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services"
        ),
    };

    public AssociationScanner(string softwareName, string exePath, string exeDir)
    {
        _softwareName = softwareName;
        _exePath = exePath;
        _exeDir = exeDir;
    }

    public ScanResult Scan(IProgress<string>? progress = null)
    {
        var result = new ScanResult();

        progress?.Report("扫描注册表...");
        ScanRegistry(result);

        progress?.Report("扫描用户数据目录...");
        ScanUserDataFolders(result);

        progress?.Report("扫描配置文件...");
        ScanConfigFiles(result);

        return result;
    }

    private void ScanRegistry(ScanResult result)
    {
        foreach (var (hive, root, display) in RegistryRoots)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var subKey = baseKey.OpenSubKey(root);
                if (subKey == null)
                    continue;
                SearchRegistryKey(subKey, display, result.RegistryKeys, _softwareName, _exePath);
            }
            catch { }
        }
    }

    private void SearchRegistryKey(
        RegistryKey key,
        string path,
        List<string> found,
        string name,
        string exePath
    )
    {
        try
        {
            if (key.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                if (!found.Contains(key.Name))
                    found.Add(key.Name);
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                try
                {
                    var value = key.GetValue(valueName)?.ToString() ?? "";
                    if (
                        value.Contains(exePath, StringComparison.OrdinalIgnoreCase)
                        || value.Contains(name, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        if (!found.Contains(key.Name))
                            found.Add(key.Name);
                        return;
                    }
                }
                catch { }
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey != null)
                        SearchRegistryKey(subKey, $@"{path}\{subKeyName}", found, name, exePath);
                }
                catch { }
            }
        }
        catch { }
    }

    private void ScanUserDataFolders(ScanResult result)
    {
        foreach (var baseDir in UserDataDirs)
        {
            if (!Directory.Exists(baseDir))
                continue;
            try
            {
                foreach (var dir in Directory.GetDirectories(baseDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.Contains(_softwareName, StringComparison.OrdinalIgnoreCase))
                        if (!result.Folders.Contains(dir))
                            result.Folders.Add(dir);
                }
            }
            catch { }
        }
    }

    private void ScanConfigFiles(ScanResult result)
    {
        if (!Directory.Exists(_exeDir))
            return;
        string[] configExtensions =
        {
            ".ini",
            ".cfg",
            ".config",
            ".json",
            ".xml",
            ".yaml",
            ".yml",
            ".log",
        };
        try
        {
            foreach (var file in Directory.GetFiles(_exeDir))
            {
                var ext = Path.GetExtension(file).ToLower();
                if (Array.Exists(configExtensions, e => e == ext))
                    if (!result.Files.Contains(file))
                        result.Files.Add(file);
            }
        }
        catch { }
    }
}
