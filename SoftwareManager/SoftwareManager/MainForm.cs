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

        public MainForm()
        {
            InitializeComponent();

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

        private void RenderList()
        {
            _listView.Items.Clear();
            foreach (var pkg in _serverList)
            {
                var installed = _installedList.FirstOrDefault(r => r.Id == pkg.Id);
                var installedVer = installed?.Version ?? "-";
                var isInstalled = installed != null;
                var needsUpdate = isInstalled && installed!.Version != pkg.Version;

                string statusText = isInstalled ? (needsUpdate ? "有更新" : "已最新") : "未安装";

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
            var pkg = SelectedPackage();
            if (pkg == null)
            {
                _btnInstall.Enabled = _btnReinstall.Enabled = _btnUninstall.Enabled = false;
                return;
            }

            var installed = _installedList.FirstOrDefault(r => r.Id == pkg.Id);
            _btnInstall.Enabled = installed == null;
            _btnReinstall.Enabled = installed != null;
            _btnUninstall.Enabled = installed != null;
        }

        private async Task DoInstallAsync(bool isReinstall)
        {
            var pkg = SelectedPackage();
            if (pkg == null)
                return;

            var action = isReinstall ? "重新安装" : "安装";
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
                Size = new Size(420, 200),
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
                Location = new Point(270, 135),
                Width = 110,
                BackColor = Color.FromArgb(26, 110, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            btnSave.Click += (_, _) =>
            {
                _config.ServerUrl = txtUrl.Text.TrimEnd('/');
                _config.InstallRoot = txtDir.Text;
                _config.Save();
                _service = new InstallService(_config);
                form.Close();
                SetStatus("设置已保存");
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
            _btnInstall.Enabled = _btnReinstall.Enabled = _btnUninstall.Enabled = !busy;
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
    }
}
