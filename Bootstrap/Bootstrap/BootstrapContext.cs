using System.IO.Compression;
using System.Text.Json;

namespace Bootstrap
{
    class BootstrapContext : ApplicationContext
    {
        private readonly string _appArg;

        public BootstrapContext(string appArg)
        {
            _appArg = appArg;

            // 用一个隐藏窗体来驱动消息泵和异步任务
            var driver = new DriverForm();
            driver.Load += async (_, _) => await RunAsync();
            driver.Show();
            driver.Hide();
        }

        async Task RunAsync()
        {
            AppHelper.LoadConfig();

            try
            {
                Directory.CreateDirectory(AppHelper.InstallDir);
            }
            catch
            {
                MessageBox.Show(
                    "无法创建目录，请以管理员身份运行。",
                    "权限不足",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
                return;
            }

            AppHelper.WriteConfig();
            CopySelfToInstallDir();

            var (updaterReady, offline) = await EnsureUpdaterAsync();
            if (!updaterReady)
            {
                Application.Exit();
                return;
            }

            var launchArg = offline ? $"{_appArg} --offline" : _appArg;
            AppHelper.LaunchUpdater(launchArg);
            Application.Exit();
        }

        void CopySelfToInstallDir()
        {
            try
            {
                var selfPath = Environment.ProcessPath!;
                var selfDir = Path.GetDirectoryName(selfPath)!;
                var bootstrapDest = Path.Combine(AppHelper.InstallDir, "Bootstrap.exe");

                if (!string.Equals(selfPath, bootstrapDest, StringComparison.OrdinalIgnoreCase))
                    File.Copy(selfPath, bootstrapDest, overwrite: true);

                var iniSrc = Path.Combine(selfDir, "config.ini");
                var iniDest = Path.Combine(AppHelper.InstallDir, "config.ini");
                if (
                    File.Exists(iniSrc)
                    && !string.Equals(iniSrc, iniDest, StringComparison.OrdinalIgnoreCase)
                )
                    File.Copy(iniSrc, iniDest, overwrite: true);
            }
            catch { }
        }

        async Task<(bool ready, bool offline)> EnsureUpdaterAsync()
        {
            SoftwareInfo? serverInfo = null;
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(AppHelper.ServerUrl) };
                http.Timeout = TimeSpan.FromSeconds(10);
                var json = await http.GetStringAsync("/api/software/updater/info");
                serverInfo = JsonSerializer.Deserialize<SoftwareInfo>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch
            {
                if (File.Exists(AppHelper.UpdaterPath))
                {
                    MessageBox.Show(
                        "服务器离线，将跳过在线更新检查。",
                        "提示",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return (true, true);
                }

                MessageBox.Show(
                    $"无法连接到服务器：{AppHelper.ServerUrl}\n\nUpdater 不存在，无法启动。",
                    "连接失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return (false, false);
            }

            if (serverInfo == null)
            {
                MessageBox.Show(
                    "服务器上找不到 Updater，请联系管理员。",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return (false, false);
            }

            var localVersion = AppHelper.GetLocalUpdaterVersion();
            if (localVersion == serverInfo.Version && File.Exists(AppHelper.UpdaterPath))
                return (true, false);

            var success = await DownloadAndExtractUpdaterAsync(serverInfo);
            return (success, false);
        }

        async Task<bool> DownloadAndExtractUpdaterAsync(SoftwareInfo info)
        {
            var form = new ProgressForm();
            form.Show();

            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(AppHelper.ServerUrl) };
                http.Timeout = TimeSpan.FromMinutes(5);

                form.SetProgress(0, $"正在下载 Updater v{info.Version}...");

                var tempZip = Path.Combine(Path.GetTempPath(), $"updater_{info.Version}.zip");
                using (
                    var response = await http.GetAsync(
                        "/api/software/updater/download",
                        HttpCompletionOption.ResponseHeadersRead
                    )
                )
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? info.FileSize;

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
                            var pct = (int)(downloaded * 80 / total);
                            form.SetProgress(
                                pct,
                                $"下载中... {downloaded / 1024:N0} KB / {total / 1024:N0} KB"
                            );
                        }
                    }
                }

                form.SetProgress(85, "解压中...");
                var tempDir = Path.Combine(Path.GetTempPath(), $"updater_{info.Version}");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                ZipFile.ExtractToDirectory(tempZip, tempDir);
                File.Delete(tempZip);

                form.SetProgress(95, "安装中...");
                foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(tempDir, file);
                    var destPath = Path.Combine(AppHelper.InstallDir, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(file, destPath, overwrite: true);
                }
                Directory.Delete(tempDir, true);

                AppHelper.SaveLocalUpdaterVersion(info.Version);

                form.SetProgress(100, "完成！");
                await Task.Delay(300);
                form.Close();
                return true;
            }
            catch (Exception ex)
            {
                form.Close();
                MessageBox.Show(
                    $"下载 Updater 失败：\n{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return false;
            }
        }
    }
}
