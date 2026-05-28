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
            _bar.Location = new Point(21, 49);
            _bar.Margin = new Padding(4, 4, 4, 4);
            _bar.Name = "_bar";
            _bar.Size = new Size(458, 14);
            _bar.TabIndex = 0;
            // 
            // _lblMsg
            // 
            _lblMsg.AutoSize = true;
            _lblMsg.Location = new Point(21, 16);
            _lblMsg.Margin = new Padding(4, 0, 4, 0);
            _lblMsg.Name = "_lblMsg";
            _lblMsg.Size = new Size(53, 20);
            _lblMsg.TabIndex = 1;
            _lblMsg.Text = "label1";
            // 
            // ProgressForm
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(494, 84);
            Controls.Add(_lblMsg);
            Controls.Add(_bar);
            Margin = new Padding(4, 4, 4, 4);
            Name = "ProgressForm";
            Text = "Bootstrap";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ProgressBar _bar;
        private Label _lblMsg;
    }
}
