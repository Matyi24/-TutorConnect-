namespace TutorConnectApp
{
    partial class TutorMain
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
            navBar = new NavBar();
            contentPanel = new Panel();
            pageTitleLabel = new Label();
            contentPanel.SuspendLayout();
            SuspendLayout();
            // 
            // navBar
            // 
            navBar.BackColor = Color.FromArgb(59, 91, 219);
            navBar.Dock = DockStyle.Left;
            navBar.Location = new Point(0, 0);
            navBar.Name = "navBar";
            navBar.Size = new Size(220, 450);
            navBar.TabIndex = 1;
            // 
            // contentPanel
            // 
            contentPanel.BackColor = Color.FromArgb(219, 228, 255);
            contentPanel.Controls.Add(pageTitleLabel);
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Location = new Point(220, 0);
            contentPanel.Name = "contentPanel";
            contentPanel.Padding = new Padding(32);
            contentPanel.Size = new Size(580, 450);
            contentPanel.TabIndex = 0;
            // 
            // pageTitleLabel
            // 
            pageTitleLabel.AutoSize = true;
            pageTitleLabel.Dock = DockStyle.Top;
            pageTitleLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
            pageTitleLabel.ForeColor = Color.FromArgb(33, 37, 41);
            pageTitleLabel.Location = new Point(32, 32);
            pageTitleLabel.Name = "pageTitleLabel";
            pageTitleLabel.Size = new Size(0, 37);
            pageTitleLabel.TabIndex = 0;
            // 
            // TutorMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(800, 450);
            Controls.Add(contentPanel);
            Controls.Add(navBar);
            Name = "TutorMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "TutorConnect";
            WindowState = FormWindowState.Maximized;
            contentPanel.ResumeLayout(false);
            contentPanel.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private NavBar navBar;
        private Panel contentPanel;
        private Label pageTitleLabel;
    }
}
