using System.Diagnostics;
using System.IO.Compression;
using SoftwareManager.Models;
using SoftwareManager.Services;

namespace SoftwareManager
{
    public partial class MainForm : Form
    {
        private AppConfig _config = AppConfig.Load();
        private InstallService _service = null!;

        private List<SoftwarePackage> _serverList = [];
        private List<InstalledRecord> _installedList = [];

        private int _sortColumn = 3; // 默认按"状态"列排序
        private bool _sortAscending = true;

        // 添加状态标记
        private bool _isBusy = false;

        public MainForm()
        {
            InitializeComponent();

            // 修复按钮位置随窗口宽度变化
            toolbar.Resize += (_, _) => RepositionToolbarButtons();
            RepositionToolbarButtons();

            // 列表列
            _listView.Columns.Add("软件名称", 200);
            _listView.Columns.Add("已安装版本", 110);
            _listView.Columns.Add("最新版本", 110);
            _listView.Columns.Add("状态", 90);
            _listView.Columns.Add("描述", 230);

            // 事件绑定
            _btnRefresh.Click += async (_, _) => await RefreshListAsync();
            btnSettings.Click += (_, _) => ShowSettings();
            _btnInstall.Click += async (_, _) => await DoInstallAsync(false);
            _btnReinstall.Click += async (_, _) => await DoInstallAsync(true);
            _btnUninstall.Click += (_, _) => DoUninstall();
            _listView.SelectedIndexChanged += (_, _) => UpdateButtons();
            _listView.ColumnClick += (_, e) =>
            {
                if (_sortColumn == e.Column)
                    _sortAscending = !_sortAscending;
                else
                {
                    _sortColumn = e.Column;
                    _sortAscending = true;
                }
                RenderList();
            };

            // 按钮初始状态
            _btnInstall.Enabled = false;
            _btnReinstall.Enabled = false;
            _btnUninstall.Enabled = false;

            // 启动
            _service = new InstallService(_config);
            _ = RefreshListAsync();
        }

        private async Task RefreshListAsync()
        {
            SetStatus("正在连接服务器...");
            _btnRefresh.Enabled = false;
            try
            {
                _serverList = await _service.GetServerListAsync();
                _installedList = _service.GetInstalledRecords();
                RenderList();
                SetStatus($"已加载 {_serverList.Count} 个软件");
            }
            catch (Exception ex)
            {
                SetStatus("连接失败：" + ex.Message);
                MessageBox.Show($"错误：{ex.Message}");
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        // 状态排序优先级：有更新=0, 未安装=1, 已最新=2
        private static int StatusOrder(string status) =>
            status switch
            {
                "有更新" => 0,
                "未安装" => 1,
                "已最新" => 2,
                _ => 9,
            };

        private void RenderList()
        {
            // 先构建带状态的数据
            var rows = _serverList
                .Select(pkg =>
                {
                    var installed = _installedList.FirstOrDefault(r => r.Id == pkg.Id);

                    // Bootstrap 特殊处理
                    var isBootstrap = pkg.Id.Equals(
                        "bootstrap",
                        StringComparison.OrdinalIgnoreCase
                    );
                    var bootstrapExists = isBootstrap && File.Exists(_config.BootstrapPath);

                    var installedVer = isBootstrap
                        ? (bootstrapExists ? pkg.Version : "-")
                        : (installed?.Version ?? "-");

                    var isInstalled = isBootstrap ? bootstrapExists : installed != null;

                    var needsUpdate =
                        isInstalled
                        && (
                            isBootstrap
                                ? false // Bootstrap 不检查版本对比
                                : installed!.Version != pkg.Version
                        );

                    var statusText = isBootstrap
                        ? "已最新"
                        : (isInstalled ? (needsUpdate ? "有更新" : "已最新") : "未安装");

                    return (pkg, installedVer, statusText);
                })
                .ToList();

            // 排序
            rows = _sortColumn switch
            {
                0 => _sortAscending
                    ? rows.OrderBy(r => r.pkg.Name).ToList()
                    : rows.OrderByDescending(r => r.pkg.Name).ToList(),
                1 => _sortAscending
                    ? rows.OrderBy(r => r.installedVer).ToList()
                    : rows.OrderByDescending(r => r.installedVer).ToList(),
                2 => _sortAscending
                    ? rows.OrderBy(r => r.pkg.Version).ToList()
                    : rows.OrderByDescending(r => r.pkg.Version).ToList(),
                3 => _sortAscending
                    ? rows.OrderBy(r => StatusOrder(r.statusText)).ToList()
                    : rows.OrderByDescending(r => StatusOrder(r.statusText)).ToList(),
                4 => _sortAscending
                    ? rows.OrderBy(r => r.pkg.Description).ToList()
                    : rows.OrderByDescending(r => r.pkg.Description).ToList(),
                _ => rows,
            };

            // 更新列标题，显示排序方向箭头
            for (int i = 0; i < _listView.Columns.Count; i++)
            {
                var col = _listView.Columns[i];
                var baseName = col.Text.TrimEnd(' ', '▲', '▼');
                col.Text = i == _sortColumn ? baseName + (_sortAscending ? " ▲" : " ▼") : baseName;
            }

            _listView.Items.Clear();
            foreach (var (pkg, installedVer, statusText) in rows)
            {
                var item = new ListViewItem(pkg.Name);
                item.SubItems.Add(installedVer);
                item.SubItems.Add(pkg.Version);
                item.SubItems.Add(statusText);
                item.SubItems.Add(pkg.Description);
                item.Tag = pkg;
                _listView.Items.Add(item);
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            // 如果正在忙，不更新按钮状态（保持禁用）
            if (_isBusy)
            {
                _btnInstall.Enabled = false;
                _btnReinstall.Enabled = false;
                _btnUninstall.Enabled = false;
                return;
            }

            var pkg = SelectedPackage();
            if (pkg == null)
            {
                _btnInstall.Enabled = _btnReinstall.Enabled = _btnUninstall.Enabled = false;
                return;
            }

            if (pkg.Id.Equals("bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                _btnInstall.Enabled = false; // 禁止安装
                _btnUninstall.Enabled = false; // 禁止卸载
                _btnReinstall.Enabled = true; // 只允许重装
                return;
            }

            var installed = _installedList.FirstOrDefault(r => r.Id == pkg.Id);
            var isSystemComponent = InstallService.SystemIds.Contains(pkg.Id);
            _btnInstall.Enabled = installed == null;
            _btnReinstall.Enabled = installed != null;
            _btnUninstall.Enabled = installed != null && !isSystemComponent;
        }

        private async Task DoInstallAsync(bool isReinstall)
        {
            var pkg = SelectedPackage();
            if (pkg == null)
                return;

            var action = isReinstall ? "重新安装" : "安装"; // ← 先声明 action

            if (pkg.Id.Equals("bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                await DoBootstrapReinstall(pkg);
                return;
            }

            // softwaremanager/updater 重装
            if (
                pkg.Id.Equals("softwaremanager", StringComparison.OrdinalIgnoreCase)
                || pkg.Id.Equals("updater", StringComparison.OrdinalIgnoreCase)
            )
            {
                DoSelfUpdate(pkg.Id);
                return;
            }

            // 检查软件是否在运行
            if (IsSoftwareRunning(pkg))
            {
                var result = MessageBox.Show(
                    $"「{pkg.Name}」正在运行，是否关闭并继续{action}？", // ← 后使用
                    "确认关闭",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result != DialogResult.Yes)
                    return;

                if (!TryCloseSoftware(pkg))
                {
                    MessageBox.Show("无法关闭正在运行的软件，请手动关闭后重试。", "提示");
                    return;
                }
            }
            if (
                MessageBox.Show(
                    $"确定要{action}「{pkg.Name}」吗？",
                    action,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                ) != DialogResult.Yes
            )
                return;

            SetBusy(true);
            _progressBar.Value = 0;

            var progress = new Progress<(int percent, string status)>(p =>
            {
                _progressBar.Value = Math.Min(p.percent, 100);
                _lblProgress.Text = p.status;
            });

            try
            {
                await _service.InstallAsync(pkg, progress);
                _installedList = _service.GetInstalledRecords();
                RenderList();
                MessageBox.Show(
                    $"「{pkg.Name}」{action}完成！\n桌面快捷方式已创建。",
                    "完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                SetStatus($"已{action} {pkg.Name}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"{action}失败：\n{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                SetBusy(false);
                _lblProgress.Text = "";
                _progressBar.Value = 0;
            }
        }

        private void DoUninstall()
        {
            var pkg = SelectedPackage();
            if (pkg == null)
                return;

            if (
                MessageBox.Show(
                    $"确定要卸载「{pkg.Name}」吗？\n软件目录和桌面快捷方式都会被删除。",
                    "确认卸载",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                ) != DialogResult.Yes
            )
                return;

            try
            {
                _service.Uninstall(pkg.Id);
                _installedList = _service.GetInstalledRecords();
                RenderList();
                SetStatus($"已卸载 {pkg.Name}");

                MessageBox.Show(
                    $"「{pkg.Name}」已成功卸载。",
                    "卸载完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"卸载失败：{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ShowSettings()
        {
            var form = new Form
            {
                Text = "设置",
                Size = new Size(420, 250),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Microsoft YaHei", 9f),
            };

            var lblUrl = new Label
            {
                Text = "服务器地址：",
                Location = new Point(20, 24),
                AutoSize = true,
            };
            var txtUrl = new TextBox
            {
                Text = _config.ServerUrl,
                Location = new Point(20, 44),
                Width = 360,
            };
            var lblDir = new Label
            {
                Text = "软件安装目录：",
                Location = new Point(20, 80),
                AutoSize = true,
            };
            var txtDir = new TextBox
            {
                Text = _config.InstallRoot,
                Location = new Point(20, 100),
                Width = 360,
            };

            var btnSave = new Button
            {
                Text = "保存",
                Width = 110,
                Height = 30,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(26, 110, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };

            // 位置让窗体自己算
            btnSave.Location = new Point(
                form.ClientSize.Width - btnSave.Width - 16,
                form.ClientSize.Height - btnSave.Height - 16
            );
            btnSave.Click += async (_, _) =>
            {
                _config.ServerUrl = txtUrl.Text.TrimEnd('/');
                _config.InstallRoot = txtDir.Text;
                // FirstInitialized 由 Bootstrap 写入，这里不允许清除
                _config.FirstInitialized = true;
                _config.Save();
                _service = new InstallService(_config);
                form.Close();
                SetStatus("设置已保存");
                await RefreshListAsync();
            };

            form.Controls.AddRange([lblUrl, txtUrl, lblDir, txtDir, btnSave]);
            form.ShowDialog(this);
        }

        private SoftwarePackage? SelectedPackage()
        {
            if (_listView.SelectedItems.Count == 0)
                return null;
            return _listView.SelectedItems[0].Tag as SoftwarePackage;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            _btnInstall.Enabled = !busy;
            _btnReinstall.Enabled = !busy;
            _btnUninstall.Enabled = !busy; // _busy 时全部禁用
            _btnRefresh.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void SetStatus(string msg) => Text = $"软件管理器 — {msg}";

        private static Button MakeButton(string text, Color backColor)
        {
            return new Button
            {
                Text = text,
                Size = new Size(96, 30),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei", 9f),
            };
        }

        private void RepositionToolbarButtons()
        {
            btnSettings.Location = new Point(toolbar.Width - btnSettings.Width - 12, 9);
            _btnRefresh.Location = new Point(
                toolbar.Width - btnSettings.Width - _btnRefresh.Width - 24,
                9
            );
        }

        // 检查软件是否在运行
        private bool IsSoftwareRunning(SoftwarePackage pkg)
        {
            if (string.IsNullOrEmpty(pkg.ExeName))
                return false;

            var processName = Path.GetFileNameWithoutExtension(pkg.ExeName);
            return Process.GetProcessesByName(processName).Length > 0;
        }

        // 尝试关闭软件
        private bool TryCloseSoftware(SoftwarePackage pkg)
        {
            try
            {
                if (string.IsNullOrEmpty(pkg.ExeName))
                    return false;

                var processName = Path.GetFileNameWithoutExtension(pkg.ExeName);
                var processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    proc.CloseMainWindow();
                    if (!proc.WaitForExit(5000))
                    {
                        proc.Kill();
                    }
                    proc.Dispose();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DoSelfUpdate(string packageId)
        {
            var result = MessageBox.Show(
                $"「{packageId}」需要重启以完成更新。",
                "需要重启",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information
            );

            if (result != DialogResult.OK)
                return;

            var appsDir = _config.InstallRoot;
            var bootstrapPath = _config.BootstrapPath;
            var targetDir = Path.Combine(appsDir, packageId);

            if (!File.Exists(bootstrapPath))
            {
                MessageBox.Show($"Bootstrap not found: {bootstrapPath}", "Error");
                return;
            }

            // BAT 放到 Temp 目录，并先 cd 到根目录
            var batPath = Path.Combine(Path.GetTempPath(), $"update_{packageId}.bat");

            // 使用 handle.exe 解锁，更可靠
            var batContent =
                $@"
@echo off
cd /d {Path.GetTempPath()}
""{Path.Combine(_config.InstallDir, "handle.exe")}"" -accepteula ""{targetDir}"" -c -y >nul 2>&1
timeout /t 1 /nobreak >nul
rd /S /Q ""{targetDir}"" >nul 2>&1
start """" ""{_config.BootstrapPath}""
del ""%~f0"" >nul 2>&1
";

            File.WriteAllText(batPath, batContent, new System.Text.UTF8Encoding(false));

            // 关键：设置 WorkingDirectory 为 Temp，避免占用目标目录
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"{batPath}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetTempPath(), // ← 在这里执行，不占目标目录
                }
            );

            Application.Exit();
        }

        // Bootstrap 重装（精简版）
        private async Task DoBootstrapReinstall(SoftwarePackage pkg)
        {
            var result = MessageBox.Show(
                $"确定要重新安装 Bootstrap 吗？\n版本：{pkg.Version}",
                "重新安装 Bootstrap",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information
            );
            if (result != DialogResult.OK)
                return;

            SetBusy(true);
            _progressBar.Value = 0;

            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"bootstrap_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                // 下载
                _lblProgress.Text = "下载中...";
                using var http = new HttpClient { BaseAddress = new Uri(_config.ServerUrl) };
                var zipPath = Path.Combine(tempDir, "bootstrap.zip");
                var response = await http.GetAsync($"/api/software/{pkg.Id}/download");
                await using (var fs = new FileStream(zipPath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                // 解压
                _progressBar.Value = 50;
                _lblProgress.Text = "解压中...";
                System.Text.Encoding.RegisterProvider(
                    System.Text.CodePagesEncodingProvider.Instance
                );
                ZipFile.ExtractToDirectory(
                    zipPath,
                    tempDir,
                    System.Text.Encoding.GetEncoding("GBK")
                );
                File.Delete(zipPath);

                // 安装
                _progressBar.Value = 70;
                _lblProgress.Text = "安装中...";
                await Task.Run(() => InstallBootstrap(tempDir));

                _progressBar.Value = 100;
                MessageBox.Show(
                    "Bootstrap 重新安装完成！",
                    "完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"失败：{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                SetBusy(false);
                _progressBar.Value = 0;
                _lblProgress.Text = "";

                _btnInstall.Enabled = false;
                _btnReinstall.Enabled = true;
                _btnUninstall.Enabled = false;
            }
        }

        // 安装 Bootstrap 文件
        private void InstallBootstrap(string sourceDir)
        {
            var targetDir = _config.InstallDir;
            // exclude 只保留用户数据类文件
            var exclude = new[] { "config.json", "installed.json", "launch.log", "error.log" };

            // 解锁时单独处理 handle.exe：它可能正被占用，先跳过
            var handlePath = Path.Combine(targetDir, "handle.exe");
            if (File.Exists(handlePath))
            {
                foreach (var file in Directory.GetFiles(targetDir))
                {
                    if (exclude.Contains(Path.GetFileName(file)))
                        continue;
                    if (Path.GetFileName(file) == "handle.exe")
                        continue; // 自己不能解锁自己
                    TryUnlock(handlePath, file);
                }
            }

            // 获取源文件（排除 apps 目录）
            var sourceFiles = Directory
                .GetFiles(sourceDir)
                .Where(f => !exclude.Contains(Path.GetFileName(f)))
                .ToList();

            // 删除旧文件
            foreach (var file in Directory.GetFiles(targetDir))
            {
                var name = Path.GetFileName(file);
                if (exclude.Contains(name))
                    continue;
                if (sourceFiles.Any(s => Path.GetFileName(s) == name))
                    continue;
                TryDelete(file);
            }

            // 复制新文件
            foreach (var file in sourceFiles)
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
            }

            Directory.Delete(sourceDir, true);
        }

        private void TryUnlock(string handlePath, string filePath)
        {
            try
            {
                Process
                    .Start(
                        new ProcessStartInfo
                        {
                            FileName = handlePath,
                            Arguments = $"-accepteula \"{filePath}\" -c -y",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                        }
                    )
                    ?.WaitForExit(2000);
            }
            catch { }
        }

        private void TryDelete(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch { }
        }
    }
}
