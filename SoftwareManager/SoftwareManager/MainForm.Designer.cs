namespace SoftwareManager
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            statusStrip = new StatusStrip();
            toolStripStatusLabel = new ToolStripStatusLabel();
            toolbar = new Panel();
            btnSettings = new Button();
            _btnRefresh = new Button();
            lblTitle = new Label();
            bottomPanel = new Panel();
            _lblProgress = new Label();
            _progressBar = new ProgressBar();
            _btnUninstall = new Button();
            _btnReinstall = new Button();
            _btnInstall = new Button();
            _listView = new ListView();
            statusStrip.SuspendLayout();
            toolbar.SuspendLayout();
            bottomPanel.SuspendLayout();
            SuspendLayout();

            // statusStrip
            statusStrip.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel });
            statusStrip.Name = "statusStrip";
            statusStrip.TabIndex = 0;

            // toolStripStatusLabel
            toolStripStatusLabel.Name = "toolStripStatusLabel";
            toolStripStatusLabel.Text = "就绪";

            // toolbar
            toolbar.Controls.Add(btnSettings);
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(lblTitle);
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 48;
            toolbar.BackColor = Color.FromArgb(26, 26, 46);
            toolbar.Name = "toolbar";
            toolbar.TabIndex = 1;

            // lblTitle
            lblTitle.Text = "🖥️  软件管理器";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(12, 12);
            lblTitle.Name = "lblTitle";
            lblTitle.TabIndex = 0;

            // _btnRefresh
            _btnRefresh.Text = "刷新";
            _btnRefresh.Size = new Size(96, 30);
            _btnRefresh.Location = new Point(484, 9);
            _btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.BackColor = Color.FromArgb(70, 90, 120);
            _btnRefresh.ForeColor = Color.White;
            _btnRefresh.FlatStyle = FlatStyle.Flat;
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Cursor = Cursors.Hand;
            _btnRefresh.Font = new Font("Microsoft YaHei", 9f);
            _btnRefresh.Name = "_btnRefresh";
            _btnRefresh.TabIndex = 1;

            // btnSettings
            btnSettings.Text = "设置";
            btnSettings.Size = new Size(96, 30);
            btnSettings.Location = new Point(592, 9);
            btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSettings.BackColor = Color.FromArgb(70, 90, 120);
            btnSettings.ForeColor = Color.White;
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Cursor = Cursors.Hand;
            btnSettings.Font = new Font("Microsoft YaHei", 9f);
            btnSettings.Name = "btnSettings";
            btnSettings.TabIndex = 2;

            // bottomPanel
            bottomPanel.Controls.Add(_lblProgress);
            bottomPanel.Controls.Add(_progressBar);
            bottomPanel.Controls.Add(_btnUninstall);
            bottomPanel.Controls.Add(_btnReinstall);
            bottomPanel.Controls.Add(_btnInstall);
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.Height = 88;
            bottomPanel.BackColor = Color.FromArgb(248, 249, 250);
            bottomPanel.Name = "bottomPanel";
            bottomPanel.TabIndex = 2;

            // _btnInstall
            _btnInstall.Text = "安装";
            _btnInstall.Size = new Size(96, 30);
            _btnInstall.Location = new Point(12, 14);
            _btnInstall.BackColor = Color.FromArgb(26, 110, 232);
            _btnInstall.ForeColor = Color.White;
            _btnInstall.FlatStyle = FlatStyle.Flat;
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Cursor = Cursors.Hand;
            _btnInstall.Font = new Font("Microsoft YaHei", 9f);
            _btnInstall.Enabled = false;
            _btnInstall.Name = "_btnInstall";
            _btnInstall.TabIndex = 3;

            // _btnReinstall
            _btnReinstall.Text = "重新安装";
            _btnReinstall.Size = new Size(96, 30);
            _btnReinstall.Location = new Point(120, 14);
            _btnReinstall.BackColor = Color.FromArgb(250, 140, 0);
            _btnReinstall.ForeColor = Color.White;
            _btnReinstall.FlatStyle = FlatStyle.Flat;
            _btnReinstall.FlatAppearance.BorderSize = 0;
            _btnReinstall.Cursor = Cursors.Hand;
            _btnReinstall.Font = new Font("Microsoft YaHei", 9f);
            _btnReinstall.Enabled = false;
            _btnReinstall.Name = "_btnReinstall";
            _btnReinstall.TabIndex = 4;

            // _btnUninstall
            _btnUninstall.Text = "卸载";
            _btnUninstall.Size = new Size(96, 30);
            _btnUninstall.Location = new Point(228, 14);
            _btnUninstall.BackColor = Color.FromArgb(220, 53, 69);
            _btnUninstall.ForeColor = Color.White;
            _btnUninstall.FlatStyle = FlatStyle.Flat;
            _btnUninstall.FlatAppearance.BorderSize = 0;
            _btnUninstall.Cursor = Cursors.Hand;
            _btnUninstall.Font = new Font("Microsoft YaHei", 9f);
            _btnUninstall.Enabled = false;
            _btnUninstall.Name = "_btnUninstall";
            _btnUninstall.TabIndex = 5;

            // _progressBar
            _progressBar.Location = new Point(12, 52);
            _progressBar.Size = new Size(500, 10);
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Name = "_progressBar";
            _progressBar.TabIndex = 6;

            // _lblProgress
            _lblProgress.Location = new Point(520, 48);
            _lblProgress.AutoSize = true;
            _lblProgress.ForeColor = Color.FromArgb(80, 80, 80);
            _lblProgress.Text = "";
            _lblProgress.Name = "_lblProgress";
            _lblProgress.TabIndex = 7;

            // _listView
            _listView.Dock = DockStyle.Fill;
            _listView.FullRowSelect = true;
            _listView.GridLines = true;
            _listView.MultiSelect = false;
            _listView.View = View.Details;
            _listView.Font = new Font("Microsoft YaHei", 9f);
            _listView.UseCompatibleStateImageBehavior = false;
            _listView.Name = "_listView";
            _listView.TabIndex = 3;

            // MainForm
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9f);
            Text = "软件管理器";
            Name = "MainForm";
            Controls.Add(_listView);
            Controls.Add(bottomPanel);
            Controls.Add(toolbar);
            Controls.Add(statusStrip);

            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            toolbar.ResumeLayout(false);
            toolbar.PerformLayout();
            bottomPanel.ResumeLayout(false);
            bottomPanel.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private StatusStrip statusStrip;
        private ToolStripStatusLabel toolStripStatusLabel;
        private Panel toolbar;
        private Label lblTitle;
        private Button btnSettings;
        private Button _btnRefresh;
        private Panel bottomPanel;
        private Label _lblProgress;
        private ProgressBar _progressBar;
        private Button _btnUninstall;
        private Button _btnReinstall;
        private Button _btnInstall;
        private ListView _listView;
    }
}
