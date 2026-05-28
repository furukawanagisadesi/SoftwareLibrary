using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Updater
{
    class UpdaterContext : ApplicationContext
    {
        private readonly string _appId;
        private readonly bool _offline;
        private readonly DriverForm _driver;
        private readonly string _handlePath;

        public UpdaterContext(string appId, bool offline)
        {
            _appId = appId;
            _offline = offline;
            _handlePath = Path.Combine(AppHelper.InstallDir, "handle.exe");

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
            Directory.CreateDirectory(AppHelper.InstallDir);
            File.WriteAllText(Path.Combine(AppHelper.InstallDir, "error.log"), "");

            var installPath = Path.Combine(AppHelper.InstallRoot, appId);

            // 版本统一从 installed.json 读取，不再用 version.txt
            var localVersion = AppHelper.GetLocalVersion(appId);

            if (_offline)
            {
                await LaunchApp(installPath, "", appId);
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
                // 服务器不可达，用本地已有版本直接启动
                await LaunchApp(installPath, localVersion, appId);
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
                await LaunchApp(installPath, serverInfo.ExeName, appId);
                return;
            }

            var form = new UpdateForm(serverInfo.Name, serverInfo.Version);
            form.Show();

            try
            {
                await DownloadAndExtract(appId, serverInfo, installPath, form);
                AppHelper.SaveInstalledRecord(
                    new InstalledRecord
                    {
                        Id = appId,
                        Version = serverInfo.Version,
                        InstallPath = installPath,
                        InstalledAt = DateTime.Now,
                    }
                );
                form.Close();

                if (appId == "softwaremanager")
                    CreateShortcut(
                        "软件管理器",
                        installPath,
                        serverInfo.ExeName,
                        "--app=softwaremanager"
                    );

                await LaunchApp(installPath, serverInfo.ExeName, appId);
            }
            catch (Exception ex)
            {
                form.Close();

                if (ex.Message == "__skip_update__")
                {
                    if (appId == "softwaremanager")
                    {
                        // SoftwareManager 正在运行，直接前置窗口
                        var proc = Process.GetProcessesByName("SoftwareManager").FirstOrDefault();
                        if (proc != null)
                        {
                            NativeMethods.ShowWindow(proc.MainWindowHandle, 9);
                            NativeMethods.SetForegroundWindow(proc.MainWindowHandle);
                        }
                        Application.Exit();
                        return;
                    }
                    await LaunchApp(installPath, serverInfo.ExeName, appId);
                    return;
                }

                File.WriteAllText(
                    Path.Combine(AppHelper.InstallDir, "error.log"),
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

                if (Directory.GetFiles(installPath).Length > 0)
                {
                    var result = MessageBox.Show(
                        $"更新失败：{ex.Message}\n\n是否用旧版本启动？\n\n点击「取消」可重新安装。",
                        "更新失败",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning
                    );

                    if (result == DialogResult.Yes)
                    {
                        await LaunchApp(installPath, serverInfo.ExeName, appId);
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        CleanDirectory(installPath);
                        var form2 = new UpdateForm(serverInfo.Name, serverInfo.Version);
                        form2.Show();
                        try
                        {
                            await DownloadAndExtract(appId, serverInfo, installPath, form2);
                            AppHelper.SaveInstalledRecord(
                                new InstalledRecord
                                {
                                    Id = appId,
                                    Version = serverInfo.Version,
                                    InstallPath = installPath,
                                    InstalledAt = DateTime.Now,
                                }
                            );
                            form2.Close();
                            await LaunchApp(installPath, serverInfo.ExeName, appId);
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

            var tempZip = Path.Combine(AppHelper.InstallDir, $"{appId}_{info.Version}.zip");
            var tempDir = Path.Combine(AppHelper.InstallDir, $"temp_{appId}");

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

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var gbk = System.Text.Encoding.GetEncoding("GBK");

            using (var fs = File.OpenRead(tempZip))
            using (var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(fs))
            {
                // 告诉 SharpZipLib：文件名字节优先用 UTF-8 解，失败则 fallback 到 GBK
                zip.StringCodec = ICSharpCode.SharpZipLib.Zip.StringCodec.FromCodePage(
                    gbk.CodePage
                );

                foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zip)
                {
                    if (!entry.IsFile)
                        continue;

                    var entryName = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                    var destPath = Path.Combine(tempDir, entryName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                    using var input = zip.GetInputStream(entry);
                    using var output = File.Create(destPath);
                    input.CopyTo(output);
                }
            }

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

            // 预关闭整个目录句柄
            CloseDirectoryHandles(installPath);
            Thread.Sleep(200);

            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(tempDir, file);
                var destPath = Path.Combine(installPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                // 智能复制：先尝试，失败则解锁
                try
                {
                    if (File.Exists(destPath))
                        File.SetAttributes(destPath, FileAttributes.Normal);
                    File.Copy(file, destPath, overwrite: true);
                }
                catch (IOException)
                {
                    TryCloseHandles(destPath);
                    Thread.Sleep(300);
                    File.Copy(file, destPath, overwrite: true);
                }
            }
            Directory.Delete(tempDir, true);

            form.SetProgress(100, "完成！");
            await Task.Delay(300);
        }

        async Task LaunchApp(string installPath, string exeName, string appId)
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
                Path.Combine(AppHelper.InstallDir, "launch.log"),
                $"installPath: {installPath}\nexeName: {exeName}\nexePath: {exePath}\nTime: {DateTime.Now}"
            );

            var splash = new UpdateForm(Path.GetFileNameWithoutExtension(exeName));
            splash.Show();
            splash.SetProgress(50, "正在启动...");

            Process.Start(
                new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = AppHelper.InstallDir,
                }
            );

            splash.SetProgress(100, "启动完成");
            await Task.Delay(800);
            splash.Close();

            Application.Exit();
        }

        // 统一的快捷方式创建方法，使用 COM 直接创建（不用 PowerShell 脚本）
        void CreateShortcut(string name, string installPath, string exeName, string bootstrapArgs)
        {
            var shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{name}.lnk"
            );
            var exePath = Path.Combine(installPath, exeName);

            dynamic? shell = null;
            dynamic? shortcut = null;

            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                shortcut = shell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = AppHelper.BootstrapPath;

                // ← 修复：如果 bootstrapArgs 包含空格，确保引号正确
                // bootstrapArgs 格式是：--app=xxx 或 --app=xxx --offline
                shortcut.Arguments = bootstrapArgs;
                shortcut.WorkingDirectory = AppHelper.InstallDir;
                shortcut.Description = name;
                shortcut.IconLocation = $"{exePath},0";

                shortcut.Save();
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(AppHelper.InstallDir, "error.log"),
                    $"Time: {DateTime.Now}\n创建快捷方式错误：{ex.Message}\n"
                );
            }
            finally
            {
                if (shortcut != null)
                    Marshal.ReleaseComObject(shortcut);
                if (shell != null)
                    Marshal.ReleaseComObject(shell);
            }
        }

        static void CleanDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            foreach (
                var dir in Directory
                    .GetDirectories(path, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)
            )
                Directory.Delete(dir, false);
            Directory.Delete(path, false);
        }

        // 关闭指定文件的句柄
        private bool TryCloseHandles(string filePath)
        {
            if (!File.Exists(_handlePath))
                return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _handlePath,
                    Arguments = $"-accepteula \"{filePath}\" -c -y",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(3000);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 关闭目录的所有句柄
        private void CloseDirectoryHandles(string dirPath)
        {
            if (!File.Exists(_handlePath))
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _handlePath,
                    Arguments = $"-accepteula \"{dirPath}\" -c -y",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }
            catch { }
        }
    }
}
