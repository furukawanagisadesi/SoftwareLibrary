namespace Bootstrap
{
    public partial class ProgressForm : Form
    {
        public ProgressForm()
        {
            InitializeComponent();
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
