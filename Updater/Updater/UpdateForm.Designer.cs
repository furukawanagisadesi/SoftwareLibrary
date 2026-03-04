namespace Updater
{
    partial class UpdateForm
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
            lblTitle = new Label();
            _lblMsg = new Label();
            _bar = new ProgressBar();
            SuspendLayout();

            // lblTitle
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(16, 14);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(380, 17);
            lblTitle.TabIndex = 0;
            lblTitle.Text = "";

            // _lblMsg
            _lblMsg.AutoSize = false;
            _lblMsg.Location = new Point(16, 38);
            _lblMsg.Name = "_lblMsg";
            _lblMsg.Size = new Size(380, 20);
            _lblMsg.TabIndex = 1;
            _lblMsg.Text = "准备中...";
            _lblMsg.ForeColor = Color.Gray;

            // _bar
            _bar.Location = new Point(16, 62);
            _bar.Name = "_bar";
            _bar.Size = new Size(376, 12);
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.TabIndex = 2;

            // UpdateForm
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(412, 90);
            Controls.AddRange(new Control[] { lblTitle, _lblMsg, _bar });
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            Name = "UpdateForm";
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9f);
            Text = "正在更新...";

            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblTitle;
        private Label _lblMsg;
        private ProgressBar _bar;
    }
}
