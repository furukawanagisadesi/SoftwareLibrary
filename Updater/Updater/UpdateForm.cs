namespace Updater
{
    public partial class UpdateForm : Form
    {
        public UpdateForm(string name, string newVersion)
        {
            InitializeComponent();
            Text = $"正在更新 {name}";
            lblTitle.Text = $"🔄  {name} 有新版本 v{newVersion}，正在更新...";
        }

        public void SetProgress(int percent, string message)
        {
            if (InvokeRequired)
            {
                Invoke(() => SetProgress(percent, message));
                return;
            }
            _bar.Value = Math.Min(percent, 100);
            _lblMsg.Text = message;
            Application.DoEvents();
        }
    }
}
