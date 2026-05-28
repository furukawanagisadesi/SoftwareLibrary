using SoftwareManager.Services;

namespace SoftwareManager;

public class CleanupDialog : Form
{
    private readonly ScanResult _result;
    private readonly CheckedListBox _listBox;
    private readonly Button _btnClean;
    private readonly Button _btnCancel;
    private readonly Label _lblStatus;

    public CleanupDialog(string softwareName, ScanResult result)
    {
        _result = result;

        Text = $"清理关联项 — {softwareName}";
        Size = new Size(620, 480);
        MinimumSize = new Size(500, 380);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei", 9f);

        // 顶部说明
        var lblTitle = new Label
        {
            Text = $"以下是「{softwareName}」卸载后检测到的残留注册表和文件，勾选后点击清理：",
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(10, 10, 10, 0),
            ForeColor = Color.FromArgb(60, 60, 60),
        };

        // 列表
        _listBox = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 8.5f),
            Margin = new Padding(10),
        };

        // 填充列表项
        foreach (var key in result.RegistryKeys)
            _listBox.Items.Add(new CleanupItem("注册表", key), true);
        foreach (var folder in result.Folders)
            _listBox.Items.Add(new CleanupItem("文件夹", folder), true);
        foreach (var file in result.Files)
            _listBox.Items.Add(new CleanupItem("文件", file), true);

        // 状态标签
        _lblStatus = new Label
        {
            Text = $"共 {_listBox.Items.Count} 项，全部已勾选",
            Dock = DockStyle.Bottom,
            Height = 24,
            Padding = new Padding(10, 4, 0, 0),
            ForeColor = Color.Gray,
        };

        _listBox.ItemCheck += (_, _) =>
        {
            // ItemCheck 触发时状态还没更新，延迟一帧计数
            BeginInvoke(() =>
            {
                _lblStatus.Text =
                    $"共 {_listBox.Items.Count} 项，已勾选 {_listBox.CheckedItems.Count} 项";
                _btnClean.Enabled = _listBox.CheckedItems.Count > 0;
            });
        };

        // 底部按钮栏
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(10, 8, 10, 8),
        };

        var btnSelectAll = new Button
        {
            Text = "全选",
            Size = new Size(72, 30),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 8),
        };
        btnSelectAll.Click += (_, _) =>
        {
            for (int i = 0; i < _listBox.Items.Count; i++)
                _listBox.SetItemChecked(i, true);
        };

        var btnSelectNone = new Button
        {
            Text = "全不选",
            Size = new Size(72, 30),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(90, 8),
        };
        btnSelectNone.Click += (_, _) =>
        {
            for (int i = 0; i < _listBox.Items.Count; i++)
                _listBox.SetItemChecked(i, false);
        };

        _btnCancel = new Button
        {
            Text = "跳过",
            Size = new Size(80, 30),
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
        };
        _btnCancel.Location = new Point(btnPanel.Width - _btnCancel.Width - 10, 8);
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnClean = new Button
        {
            Text = "清理勾选项",
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(200, 50, 50),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
        };
        _btnClean.FlatAppearance.BorderSize = 0;
        _btnClean.Location = new Point(btnPanel.Width - _btnClean.Width - _btnCancel.Width - 20, 8);
        _btnClean.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        btnPanel.Controls.AddRange([btnSelectAll, btnSelectNone, _btnClean, _btnCancel]);
        btnPanel.Resize += (_, _) =>
        {
            _btnCancel.Location = new Point(btnPanel.Width - _btnCancel.Width - 10, 8);
            _btnClean.Location = new Point(
                btnPanel.Width - _btnClean.Width - _btnCancel.Width - 20,
                8
            );
        };

        // 列表容器（加 padding）
        var listPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
        listPanel.Controls.Add(_listBox);

        Controls.Add(listPanel);
        Controls.Add(_lblStatus);
        Controls.Add(btnPanel);
        Controls.Add(lblTitle);
    }

    /// <summary>返回用户勾选要删除的所有项</summary>
    public List<CleanupItem> GetCheckedItems() =>
        _listBox.CheckedItems.Cast<CleanupItem>().ToList();
}

public record CleanupItem(string Category, string Path)
{
    public override string ToString() => $"[{Category}]  {Path}";
}
