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
            SuspendLayout();
            // 
            // navBar
            // 
            navBar.Dock = DockStyle.Left;
            navBar.Name = "navBar";
            navBar.Width = 220;
            // 
            // contentPanel
            // 
            contentPanel.BackColor = Theme.MainBlueLight;
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Name = "contentPanel";
            //
            // TutorMain
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            Controls.Add(contentPanel);
            Controls.Add(navBar);
            ClientSize = new Size(800, 450);
            Name = "TutorMain";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "TutorConnect";
            WindowState = FormWindowState.Maximized;
            ResumeLayout(false);
        }

        #endregion

        private NavBar navBar;
        private Panel contentPanel;
    }
}
