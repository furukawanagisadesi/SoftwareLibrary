namespace Bootstrap
{
    partial class ProgressForm
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
            _bar = new ProgressBar();
            _lblMsg = new Label();
            SuspendLayout();
            // 
            // _bar
            // 
            _bar.Location = new Point(16, 42);
            _bar.Name = "_bar";
            _bar.Size = new Size(356, 12);
            _bar.TabIndex = 0;
            // 
            // _lblMsg
            // 
            _lblMsg.AutoSize = true;
            _lblMsg.Location = new Point(16, 14);
            _lblMsg.Name = "_lblMsg";
            _lblMsg.Size = new Size(43, 17);
            _lblMsg.TabIndex = 1;
            _lblMsg.Text = "label1";
            // 
            // ProgressForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 71);
            Controls.Add(_lblMsg);
            Controls.Add(_bar);
            Name = "ProgressForm";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ProgressBar _bar;
        private Label _lblMsg;
    }
}
