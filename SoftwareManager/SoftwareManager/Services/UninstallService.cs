namespace SoftwareManager.Services;

public class UninstallService : ServiceBase
{
    public UninstallService(AppConfig config)
        : base(config) { }

    // 卸载：系统组件在调用前已被 UI 层拦截，这里不做二次检查
    // 返回 (installPath, exePath) 供调用方做关联扫描
    public (string InstallPath, string ExePath) Uninstall(string id)
    {
        var records = GetInstalledRecords();
        var rec = records.FirstOrDefault(r => r.Id == id);

        var installPath = rec?.InstallPath ?? "";
        var pkg = _serverList?.FirstOrDefault(p => p.Id == id);
        var exePath =
            (rec != null && pkg != null) ? Path.Combine(rec.InstallPath, pkg.ExeName) : "";

        if (rec != null && Directory.Exists(rec.InstallPath))
            Directory.Delete(rec.InstallPath, true);

        // 删除桌面快捷方式，按软件名查找
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        // 遍历桌面找到指向该 installPath 的 .lnk（名称可能和 id 不同）
        foreach (var lnk in Directory.GetFiles(desktopPath, "*.lnk"))
        {
            // 用文件名和 id 匹配做简单清理
            if (
                Path.GetFileNameWithoutExtension(lnk).Equals(id, StringComparison.OrdinalIgnoreCase)
            )
            {
                File.Delete(lnk);
                break;
            }
        }

        records.RemoveAll(r => r.Id == id);
        SaveRecords(records);

        return (installPath, exePath);
    }

    /// <summary>删除注册表键值（支持 HKCU/HKLM/HKCR）</summary>
    public static void DeleteRegistryKey(string fullKeyPath)
    {
        // fullKeyPath 格式: HKEY_CURRENT_USER\Software\xxx
        var sep = fullKeyPath.IndexOf('\\');
        if (sep < 0)
            return;
        var hiveName = fullKeyPath[..sep];
        var subPath = fullKeyPath[(sep + 1)..];

        var hive = hiveName switch
        {
            "HKEY_CURRENT_USER" => Microsoft.Win32.RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => Microsoft.Win32.RegistryHive.LocalMachine,
            "HKEY_CLASSES_ROOT" => Microsoft.Win32.RegistryHive.ClassesRoot,
            _ => (Microsoft.Win32.RegistryHive?)null,
        };
        if (hive == null)
            return;

        using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
            hive.Value,
            Microsoft.Win32.RegistryView.Default
        );
        baseKey.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
    }
}
