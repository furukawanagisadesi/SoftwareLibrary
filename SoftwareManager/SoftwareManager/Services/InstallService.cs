using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SoftwareManager.Models;

namespace SoftwareManager.Services;

public class InstallService
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;
    private readonly string _recordFile;

    public InstallService(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(config.ServerUrl) };
        _http.Timeout = TimeSpan.FromMinutes(30);
        var managerDir = Path.GetDirectoryName(_config.InstallRoot)!;
        _recordFile = Path.Combine(managerDir, "installed.json");
    }

    // 获取服务器软件列表
    public async Task<List<SoftwarePackage>> GetServerListAsync()
    {
        var json = await _http.GetStringAsync("/api/software/list");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<SoftwarePackage>>(json, options) ?? [];

        // 过滤掉系统组件，不在软件管理器里显示
        var systemIds = new[] { "updater", "softwaremanager" };
        return list.Where(s => !systemIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    // 读取本地已安装记录
    public List<InstalledRecord> GetInstalledRecords()
    {
        if (!File.Exists(_recordFile))
            return [];
        var json = File.ReadAllText(_recordFile);
        return JsonSerializer.Deserialize<List<InstalledRecord>>(json) ?? [];
    }

    // 下载并安装（解压）软件，progress 回调返回 0~100
    public async Task InstallAsync(
        SoftwarePackage pkg,
        IProgress<(int percent, string status)> progress
    )
    {
        var installPath = Path.Combine(_config.InstallRoot, pkg.Id);

        // 1. 下载 zip
        progress.Report((0, "开始下载..."));
        var tempZip = Path.Combine(Path.GetTempPath(), $"{pkg.Id}_{pkg.Version}.zip");

        using (
            var response = await _http.GetAsync(
                $"/api/software/{pkg.Id}/download",
                HttpCompletionOption.ResponseHeadersRead
            )
        )
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? pkg.FileSize;
            await using var fs = new FileStream(tempZip, FileMode.Create);
            await using var stream = await response.Content.ReadAsStreamAsync();

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (total > 0)
                {
                    var pct = (int)(downloaded * 50 / total); // 下载占 0~50%
                    progress.Report(
                        (pct, $"下载中 {FormatSize(downloaded)} / {FormatSize(total)}")
                    );
                }
            }
        }

        // 2. 删除旧目录，解压
        progress.Report((50, "解压中..."));
        if (Directory.Exists(installPath))
            Directory.Delete(installPath, true);
        Directory.CreateDirectory(installPath);
        ZipFile.ExtractToDirectory(tempZip, installPath);
        File.Delete(tempZip);

        progress.Report((80, "创建快捷方式..."));

        // 3. 创建桌面快捷方式（指向 Launcher.exe --app=id）
        CreateShortcut(pkg, installPath);

        // 解压成功后写入 version.txt，供 Updater 检查版本用
        File.WriteAllText(Path.Combine(installPath, "version.txt"), pkg.Version);

        // 4. 记录安装信息
        progress.Report((95, "记录安装信息..."));
        SaveRecord(
            new InstalledRecord
            {
                Id = pkg.Id,
                Version = pkg.Version,
                InstallPath = installPath,
                InstalledAt = DateTime.Now,
            }
        );

        progress.Report((100, "安装完成！"));
    }

    // 卸载（删除目录 + 快捷方式 + 记录）
    public void Uninstall(string id)
    {
        var records = GetInstalledRecords();
        var rec = records.FirstOrDefault(r => r.Id == id);
        if (rec != null && Directory.Exists(rec.InstallPath))
            Directory.Delete(rec.InstallPath, true);

        // 删除桌面快捷方式
        var shortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"{id}.lnk"
        );
        if (File.Exists(shortcut))
            File.Delete(shortcut);

        records.RemoveAll(r => r.Id == id);
        SaveRecords(records);
    }

    // 创建桌面快捷方式 → Launcher.exe --app=chrome
    private void CreateShortcut(SoftwarePackage pkg, string installPath)
    {
        var updaterDir = Path.GetDirectoryName(_config.InstallRoot)!;
        var bootstrapPath = Path.Combine(updaterDir, "Bootstrap.exe");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktopPath, $"{pkg.Name}.lnk");

        var script = $"""
$ws = New-Object -ComObject WScript.Shell
$s  = $ws.CreateShortcut('{shortcutPath}')
$s.TargetPath       = '{bootstrapPath}'
$s.Arguments        = '--app={pkg.Id}'
$s.WorkingDirectory = '{installPath}'
$s.Description      = '{pkg.Name}'
$s.IconLocation     = '{Path.Combine(installPath, pkg.ExeName)},0'
$s.Save()
""";

        var scriptPath = Path.Combine(Path.GetTempPath(), $"shortcut_{pkg.Id}.ps1");
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

        var psi = new ProcessStartInfo(
            "powershell",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\""
        )
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
        };

        var proc = Process.Start(psi);
        var error = proc?.StandardError.ReadToEnd();
        proc?.WaitForExit();
        File.Delete(scriptPath);
    }

    private void SaveRecord(InstalledRecord record)
    {
        // 每次从文件重新读取，避免覆盖其他进程写入的记录
        var records = new List<InstalledRecord>();
        if (File.Exists(_recordFile))
        {
            try
            {
                var json = File.ReadAllText(_recordFile);
                records =
                    JsonSerializer.Deserialize<List<InstalledRecord>>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    ) ?? [];
                records.RemoveAll(r => string.IsNullOrEmpty(r.Id)); // 过滤空记录
            }
            catch { }
        }

        records.RemoveAll(r => r.Id == record.Id);
        records.Add(record);
        SaveRecords(records);
    }

    private void SaveRecords(List<InstalledRecord> records)
    {
        var json = JsonSerializer.Serialize(
            records,
            new JsonSerializerOptions { WriteIndented = true }
        );
        File.WriteAllText(_recordFile, json);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1024.0 / 1024:F1} MB";
    }
}
