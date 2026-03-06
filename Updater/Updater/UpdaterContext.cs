using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Updater
{
    class UpdaterContext : ApplicationContext
    {
        private readonly string _appId;
        private readonly bool _offline;
        private readonly DriverForm _driver;

        public UpdaterContext(string appId, bool offline)
        {
            _appId = appId;
            _offline = offline;

            _driver = new DriverForm();
            _driver.Load += async (_, _) =>
            {
                AppHelper.LoadConfig();
                await CheckUpdateAndLaunch(_appId);
            };
            _driver.Show();
            _driver.Hide();
        }

        async Task CheckUpdateAndLaunch(string appId)
        {
            File.WriteAllText(Path.Combine(AppHelper.UpdaterDir, "error.log"), "");

            var installPath = Path.Combine(AppHelper.InstallRoot, appId);
            var versionFile = Path.Combine(installPath, "version.txt");
            var localVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "";

            if (_offline)
            {
                LaunchApp(installPath, "", appId);
                return;
            }

            SoftwareInfo? serverInfo = null;
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(AppHelper.ServerUrl) };
                http.Timeout = TimeSpan.FromSeconds(10);
                var json = await http.GetStringAsync($"/api/software/{appId}/info");
                serverInfo = JsonSerializer.Deserialize<SoftwareInfo>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
            }
            catch
            {
                LaunchApp(installPath, localVersion, appId);
                return;
            }

            if (serverInfo == null)
            {
                MessageBox.Show(
                    $"服务器上找不到软件：{appId}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
                return;
            }

            if (localVersion == serverInfo.Version && Directory.Exists(installPath))
            {
                LaunchApp(installPath, serverInfo.ExeName, appId);
                return;
            }

            var form = new UpdateForm(serverInfo.Name, serverInfo.Version);
            form.Show();

            try
            {
                await DownloadAndExtract(appId, serverInfo, installPath, form);
                File.WriteAllText(versionFile, serverInfo.Version);
                UpdateInstalledRecord(appId, serverInfo.Version, installPath);
                form.Close();

                if (appId == "softwaremanager")
                    CreateSoftwareManagerShortcut(installPath, serverInfo.ExeName);

                LaunchApp(installPath, serverInfo.ExeName, appId);
            }
            catch (Exception ex)
            {
                form.Close();

                if (ex.Message == "__skip_update__")
                {
                    if (appId == "softwaremanager")
                    {
                        var proc = Process.GetProcessesByName("SoftwareManager").FirstOrDefault();
                        if (proc != null)
                        {
                            NativeMethods.ShowWindow(proc.MainWindowHandle, 9);
                            NativeMethods.SetForegroundWindow(proc.MainWindowHandle);
                        }
                        Application.Exit();
                        return;
                    }
                    LaunchApp(installPath, serverInfo.ExeName, appId);
                    return;
                }

                File.WriteAllText(
                    Path.Combine(AppHelper.UpdaterDir, "error.log"),
                    $"Time: {DateTime.Now}\nAppId: {appId}\nError: {ex.Message}\nStackTrace: {ex.StackTrace}"
                );

                if (!Directory.Exists(installPath))
                {
                    MessageBox.Show(
                        $"安装失败：{ex.Message}",
                        "错误",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Application.Exit();
                    return;
                }

                if (Directory.Exists(installPath) && Directory.GetFiles(installPath).Length > 0)
                {
                    var result = MessageBox.Show(
                        $"更新失败：{ex.Message}\n\n是否用旧版本启动？\n\n点击「取消」可重新安装。",
                        "更新失败",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        LaunchApp(installPath, serverInfo.ExeName, appId);
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        if (Directory.Exists(installPath))
                        {
                            foreach (
                                var file in Directory.GetFiles(
                                    installPath,
                                    "*",
                                    SearchOption.AllDirectories
                                )
                            )
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }
                            foreach (
                                var dir in Directory
                                    .GetDirectories(installPath, "*", SearchOption.AllDirectories)
                                    .OrderByDescending(d => d.Length)
                            )
                                Directory.Delete(dir, false);
                            Directory.Delete(installPath, false);
                        }

                        var form2 = new UpdateForm(serverInfo.Name, serverInfo.Version);
                        form2.Show();
                        try
                        {
                            await DownloadAndExtract(appId, serverInfo, installPath, form2);
                            File.WriteAllText(versionFile, serverInfo.Version);
                            UpdateInstalledRecord(appId, serverInfo.Version, installPath);
                            form2.Close();
                            LaunchApp(installPath, serverInfo.ExeName, appId);
                        }
                        catch (Exception ex2)
                        {
                            form2.Close();
                            MessageBox.Show(
                                $"重新安装失败：{ex2.Message}",
                                "错误",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            Application.Exit();
                        }
                    }
                    else
                    {
                        Application.Exit();
                    }
                }
            }
        }

        async Task DownloadAndExtract(
            string appId,
            SoftwareInfo info,
            string installPath,
            UpdateForm form
        )
        {
            using var http = new HttpClient { BaseAddress = new Uri(AppHelper.ServerUrl) };
            http.Timeout = TimeSpan.FromMinutes(30);

            var tempZip = Path.Combine(AppHelper.UpdaterDir, $"{appId}_{info.Version}.zip");
            var tempDir = Path.Combine(AppHelper.UpdaterDir, $"temp_{appId}");

            using (
                var response = await http.GetAsync(
                    $"/api/software/{appId}/download",
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
                            $"下载中... {AppHelper.FormatSize(downloaded)} / {AppHelper.FormatSize(total)}"
                        );
                    }
                }
            }

            form.SetProgress(85, "解压中...");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(tempZip, tempDir);
            File.Delete(tempZip);

            form.SetProgress(92, "安装中...");

            var exeName = Path.GetFileNameWithoutExtension(info.ExeName);
            var running = Process.GetProcessesByName(exeName);
            if (running.Length > 0)
            {
                if (appId == "softwaremanager")
                {
                    Directory.Delete(tempDir, true);
                    throw new InvalidOperationException("__skip_update__");
                }

                var result = MessageBox.Show(
                    $"「{info.Name}」正在运行，需要关闭后才能更新。\n\n是否自动关闭？",
                    "软件正在运行",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.Yes)
                {
                    foreach (var p in running)
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                }
                else
                {
                    Directory.Delete(tempDir, true);
                    throw new InvalidOperationException("__skip_update__");
                }
            }

            Directory.CreateDirectory(installPath);
            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, file);
                var destPath = Path.Combine(installPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, overwrite: true);
            }
            Directory.Delete(tempDir, true);

            form.SetProgress(100, "完成！");
            await Task.Delay(300);
        }

        void LaunchApp(string installPath, string exeName, string appId)
        {
            if (!Directory.Exists(installPath))
            {
                MessageBox.Show(
                    $"软件未安装，请先用软件管理器安装「{appId}」",
                    "未安装",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                Application.Exit();
                return;
            }

            if (string.IsNullOrEmpty(exeName))
            {
                var exeFiles = Directory.GetFiles(
                    installPath,
                    "*.exe",
                    SearchOption.TopDirectoryOnly
                );
                if (exeFiles.Length == 0)
                {
                    MessageBox.Show(
                        $"在 {installPath} 中找不到可执行文件",
                        "启动失败",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Application.Exit();
                    return;
                }
                exeName = Path.GetFileName(exeFiles[0]);
            }

            var exePath = Path.Combine(installPath, exeName);
            if (!File.Exists(exePath))
            {
                MessageBox.Show(
                    $"找不到：{exePath}",
                    "启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                Application.Exit();
                return;
            }

            File.WriteAllText(
                Path.Combine(AppHelper.UpdaterDir, "launch.log"),
                $"installPath: {installPath}\nexeName: {exeName}\nexePath: {exePath}\nTime: {DateTime.Now}"
            );

            // 显示启动画面
            var splash = new UpdateForm(Path.GetFileNameWithoutExtension(exeName));
            splash.Show();
            splash.SetProgress(50, "正在启动...");
            Application.DoEvents();

            Process.Start(
                new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = installPath,
                }
            );

            splash.SetProgress(100, "启动完成");
            Task.Delay(800).Wait();
            splash.Close();

            Application.Exit();
        }

        void CreateSoftwareManagerShortcut(string installPath, string exeName)
        {
            var bootstrapPath = Path.Combine(AppHelper.UpdaterDir, "Bootstrap.exe");
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "软件管理器.lnk"
            );
            var exePath = Path.Combine(installPath, exeName);

            var script = $"""
$ws = New-Object -ComObject WScript.Shell
$s  = $ws.CreateShortcut('{shortcutPath}')
$s.TargetPath       = '{bootstrapPath}'
$s.Arguments        = '--app=softwaremanager'
$s.WorkingDirectory = '{installPath}'
$s.Description      = '软件管理器'
$s.IconLocation     = '{exePath},0'
$s.Save()
""";

            var scriptPath = Path.Combine(Path.GetTempPath(), "create_shortcut.ps1");
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

            if (!string.IsNullOrEmpty(error))
                File.AppendAllText(
                    Path.Combine(AppHelper.UpdaterDir, "error.log"),
                    $"Time: {DateTime.Now}\n创建快捷方式错误：{error}"
                );
        }

        void UpdateInstalledRecord(string appId, string version, string installPath)
        {
            try
            {
                var recordFile = Path.Combine(AppHelper.UpdaterDir, "installed.json");
                var records = new List<InstalledRecord>();

                if (File.Exists(recordFile))
                {
                    var json = File.ReadAllText(recordFile);
                    records =
                        JsonSerializer.Deserialize<List<InstalledRecord>>(
                            json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        ) ?? [];
                }

                records.RemoveAll(r => string.IsNullOrEmpty(r.Id));
                records.RemoveAll(r => r.Id == appId);
                records.Add(
                    new InstalledRecord
                    {
                        Id = appId,
                        Version = version,
                        InstallPath = installPath,
                        InstalledAt = DateTime.Now,
                    }
                );

                File.WriteAllText(
                    recordFile,
                    JsonSerializer.Serialize(
                        records,
                        new JsonSerializerOptions { WriteIndented = true }
                    )
                );
            }
            catch { }
        }
    }
}
