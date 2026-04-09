using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SoftwareManager.Models;

namespace SoftwareManager.Services;

public class InstallService
{
    private readonly AppConfig _config;
    private readonly HttpClient _http;

    // 系统组件：显示在列表里但禁止卸载
    public static readonly HashSet<string> SystemIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "bootstrap", "updater", "softwaremanager"
    };

    public InstallService(AppConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(config.ServerUrl) };
        _http.Timeout = TimeSpan.FromMinutes(30);
    }

    // 获取服务器软件列表，不再过滤系统组件
    public async Task<List<SoftwarePackage>> GetServerListAsync()
    {
        var json = await _http.GetStringAsync("/api/software/list");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<SoftwarePackage>>(json, options) ?? [];
    }

    // 读取本地已安装记录
    public List<InstalledRecord> GetInstalledRecords()
    {
        if (!File.Exists(_config.InstalledRecordPath))
            return [];

        try
        {
            var json = File.ReadAllText(_config.InstalledRecordPath);
            var records = JsonSerializer.Deserialize<List<InstalledRecord>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? [];

            // 过滤掉安装未完成的（有 .installing 标记的）
            return records
                .Where(r =>
                {
                    var mark = Path.Combine(r.InstallPath, ".installing");
                    return !File.Exists(mark);
                })
                .ToList();
        }
        catch { return []; }
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
            var sw = Stopwatch.StartNew();
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (total > 0 && sw.ElapsedMilliseconds >= 100)
                {
                    sw.Restart();
                    var pct = (int)((double)downloaded / total * 50);
                    progress.Report((pct, $"下载中 {FormatSize(downloaded)} / {FormatSize(total)}"));
                }
            }
        }

        // 2. 删除旧目录，逐条目解压
        progress.Report((50, "解压中..."));
        if (Directory.Exists(installPath))
            Directory.Delete(installPath, true);
        Directory.CreateDirectory(installPath);

        // 先写安装中标记，异常时 GetInstalledRecords 会过滤掉此条目
        var tempMark = Path.Combine(installPath, ".installing");
        File.WriteAllText(tempMark, "");

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var gbk = System.Text.Encoding.GetEncoding("GBK");

        using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Read, gbk))
        {
            var entries = zip.Entries.ToList();
            int total2 = entries.Count;
            for (int i = 0; i < total2; i++)
            {
                var entry = entries[i];
                var destPath = Path.GetFullPath(Path.Combine(installPath, entry.FullName));
                // 安全检查，防止路径穿越
                if (
                    !destPath.StartsWith(
                        Path.GetFullPath(installPath) + Path.DirectorySeparatorChar
                    )
                    && destPath != Path.GetFullPath(installPath)
                )
                    continue;

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    Directory.CreateDirectory(destPath);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                var pct = 50 + (int)((double)(i + 1) / total2 * 30);
                progress.Report((pct, $"解压中 {i + 1} / {total2}"));
            }
        }

        File.Delete(tempZip);

        // 3. 创建桌面快捷方式，系统组件快捷方式指向 Bootstrap，WorkingDirectory 为 InstallDir
        progress.Report((80, "创建快捷方式..."));
        CreateShortcut(pkg, installPath);

        // 4. 写入安装记录
        progress.Report((95, "记录安装信息..."));
        SaveRecord(new InstalledRecord
        {
            Id = pkg.Id,
            Version = pkg.Version,
            InstallPath = installPath,
            InstalledAt = DateTime.Now,
        });

        progress.Report((100, "安装完成！"));

        // 全部成功后删除安装中标记
        File.Delete(tempMark);
    }

    // 卸载：系统组件在调用前已被 UI 层拦截，这里不做二次检查
    public void Uninstall(string id)
    {
        var records = GetInstalledRecords();
        var rec = records.FirstOrDefault(r => r.Id == id);
        if (rec != null && Directory.Exists(rec.InstallPath))
            Directory.Delete(rec.InstallPath, true);

        // 删除桌面快捷方式，按软件名查找
        var pkg = rec != null
            ? Path.GetFileName(rec.InstallPath) // fallback
            : id;
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        // 遍历桌面找到指向该 installPath 的 .lnk（名称可能和 id 不同）
        foreach (var lnk in Directory.GetFiles(desktopPath, "*.lnk"))
        {
            // 用文件名和 id 匹配做简单清理
            if (Path.GetFileNameWithoutExtension(lnk)
                .Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(lnk);
                break;
            }
        }

        records.RemoveAll(r => r.Id == id);
        SaveRecords(records);
    }

    private void CreateShortcut(SoftwarePackage pkg, string installPath)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktopPath, $"{pkg.Name}.lnk");
        var exePath = Path.Combine(installPath, pkg.ExeName);

        // 所有软件的快捷方式统一指向 Bootstrap，WorkingDirectory 为 InstallDir
        var script = $"""
$ws = New-Object -ComObject WScript.Shell
$s  = $ws.CreateShortcut('{shortcutPath}')
$s.TargetPath       = '{_config.BootstrapPath}'
$s.Arguments        = '--app={pkg.Id}'
$s.WorkingDirectory = '{_config.InstallDir}'
$s.Description      = '{pkg.Name}'
$s.IconLocation     = '{exePath},0'
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
        proc?.StandardError.ReadToEnd();
        proc?.WaitForExit();
        File.Delete(scriptPath);
    }

    private void SaveRecord(InstalledRecord record)
    {
        var records = new List<InstalledRecord>();
        if (File.Exists(_config.InstalledRecordPath))
        {
            try
            {
                var json = File.ReadAllText(_config.InstalledRecordPath);
                records = JsonSerializer.Deserialize<List<InstalledRecord>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? [];
                records.RemoveAll(r => string.IsNullOrEmpty(r.Id));
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
        File.WriteAllText(_config.InstalledRecordPath, json);
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
