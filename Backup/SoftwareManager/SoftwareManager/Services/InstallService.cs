using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SoftwareManager.Models;

namespace SoftwareManager.Services;

public class InstallService : ServiceBase
{
    private readonly HttpClient _http;

    public InstallService(AppConfig config)
        : base(config)
    {
        // ⚠ 必须禁用代理，否则开启系统代理时 HttpClient 会尝试把 127.0.0.1 的请求发往代理端口导致失败
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null,
        };
        _http = new HttpClient(handler) { BaseAddress = new Uri(config.ServerUrl) };
        _http.Timeout = TimeSpan.FromMinutes(30);
    }

    // 获取服务器软件列表，不再过滤系统组件
    public async Task<List<SoftwarePackage>> GetServerListAsync()
    {
        var json = await _http.GetStringAsync("/api/software/list");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<SoftwarePackage>>(json, options) ?? [];
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
                    progress.Report(
                        (pct, $"下载中 {FormatSize(downloaded)} / {FormatSize(total)}")
                    );
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

        using (var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(tempZip))
        {
            zip.StringCodec = ICSharpCode.SharpZipLib.Zip.StringCodec.FromCodePage(gbk.CodePage);

            var entries = zip.Cast<ICSharpCode.SharpZipLib.Zip.ZipEntry>().ToList();
            int total2 = entries.Count;
            for (int i = 0; i < total2; i++)
            {
                var entry = entries[i];
                var entryName = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                var destPath = Path.GetFullPath(Path.Combine(installPath, entryName));

                // 安全检查，防止路径穿越
                if (
                    !destPath.StartsWith(
                        Path.GetFullPath(installPath) + Path.DirectorySeparatorChar
                    )
                    && destPath != Path.GetFullPath(installPath)
                )
                    continue;

                if (entry.IsDirectory)
                    Directory.CreateDirectory(destPath);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    using var input = zip.GetInputStream(entry);
                    using var output = File.Create(destPath);
                    await input.CopyToAsync(output);
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

        // 全部成功后删除安装中标记
        File.Delete(tempMark);
    }

    // 创建桌面快捷方式，使用 COM 直接创建（不用 PowerShell 脚本）
    private void CreateShortcut(SoftwarePackage pkg, string installPath)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutPath = Path.Combine(desktopPath, $"{pkg.Name}.lnk");
        var exePath = Path.Combine(installPath, pkg.ExeName);

        dynamic? shell = null;
        dynamic? shortcut = null;

        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut = shell.CreateShortcut(shortcutPath);

            shortcut.TargetPath = _config.BootstrapPath;

            // ← 修复：参数值包含空格时，用引号包裹
            var args = pkg.Id.Contains(' ')
                ? $"--app=\"{pkg.Id}\"" // 有空格：--app="clash party"
                : $"--app={pkg.Id}"; // 无空格：--app=chrome

            shortcut.Arguments = args;
            shortcut.WorkingDirectory = _config.InstallDir;
            shortcut.Description = pkg.Name;
            shortcut.IconLocation = $"{exePath},0";

            shortcut.Save();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"创建快捷方式错误：{ex.Message}");
        }
        finally
        {
            if (shortcut != null)
                Marshal.ReleaseComObject(shortcut);
            if (shell != null)
                Marshal.ReleaseComObject(shell);
        }
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
