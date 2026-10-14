using System.Drawing.Drawing2D;

namespace TutorConnectApp
{
    /// <summary>
    /// Bejelentkező ablak. Az app csak sikeres belépés (DialogResult.OK) után nyitja meg a főablakot.
    /// Kódból épül fel (nincs Designer), az ablak átméretezhető, a kártya középen marad.
    /// A háttér színátmenetes, a kártya megjelenéskor felcsúszik és beúszik.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly SurfacePanel card;
        private readonly TextBox emailBox;
        private readonly TextBox passwordBox;
        private readonly HoverButton loginButton;
        private readonly Label errorLabel;
        private int slideOffset = 36;

        public LoginForm()
        {
            Text = "TutorConnect – Bejelentkezés";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 700);
            MinimumSize = new Size(480, 640);
            Font = Ui.MakeFont(10F);
            DoubleBuffered = true;
            ResizeRedraw = true;
            Opacity = 0; // az OnShown-ban úszik be

            card = new SurfacePanel { Size = new Size(400, 500) };

            var logo = new AvatarCircle("Tutor Connect", 68) { Location = new Point((400 - 68) / 2, 32) };

            var title = Ui.Lbl("TutorConnect", 21F, Theme.Text, bold: true);
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Bounds = new Rectangle(0, 110, 400, 40);

            var subtitle = Ui.Lbl("Jelentkezz be a folytatáshoz", 10.5F, Theme.Muted);
            subtitle.AutoSize = false;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.Bounds = new Rectangle(0, 150, 400, 26);

            var emailLabel = MakeLabel("E-mail", 192);
            emailBox = MakeTextBox(216);

            var passwordLabel = MakeLabel("Jelszó", 266);
            passwordBox = MakeTextBox(290);
            passwordBox.UseSystemPasswordChar = true;

            errorLabel = Ui.Lbl("", 9.5F, Theme.Danger);
            errorLabel.AutoSize = false;
            errorLabel.Bounds = new Rectangle(32, 338, 336, 44);
            errorLabel.Visible = false;

            loginButton = Ui.PrimaryBtn("Bejelentkezés", async (_, _) => await TryLoginAsync());
            loginButton.AutoSize = false;
            loginButton.Font = Ui.MakeFont(11F, FontStyle.Bold);
            loginButton.Bounds = new Rectangle(32, 392, 336, 50);
            loginButton.Margin = new Padding(0);

            var hint = Ui.Lbl($"Szerver: {ApiClient.BaseUrl}", 8.5F, Theme.Muted);
            hint.AutoSize = false;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.Bounds = new Rectangle(0, 458, 400, 24);

            card.Controls.AddRange(new Control[] { logo, title, subtitle, emailLabel, emailBox, passwordLabel, passwordBox, errorLabel, loginButton, hint });
            Controls.Add(card);

            AcceptButton = loginButton; // Enter = bejelentkezés
            Resize += (_, _) => CenterCard();
            CenterCard();
        }

        private void CenterCard()
        {
            card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2 + slideOffset);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var area = ClientRectangle;
            if (area.Width < 2 || area.Height < 2) return;

            Color c1 = Theme.Dark ? Theme.LoginDark1 : Theme.HeroLight1;
            Color c2 = Theme.Dark ? Theme.LoginDark2 : Theme.MainBlueDark;

            var g = e.Graphics;
            using (var brush = new LinearGradientBrush(area, c1, c2, 45F))
                g.FillRectangle(brush, area);

            // díszítő körök
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var circle = new SolidBrush(Color.FromArgb(24, Color.White));
            g.FillEllipse(circle, -120, -120, 380, 380);
            g.FillEllipse(circle, area.Width - 220, area.Height - 200, 420, 420);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Anim.Run(450, t =>
            {
                Opacity = t;
                slideOffset = (int)(36 * (1 - t));
                CenterCard();
            });
            emailBox.Focus();
        }

        private static Label MakeLabel(string text, int y)
        {
            var label = Ui.Lbl(text, 10F, Theme.Muted);
            label.AutoSize = false;
            label.Bounds = new Rectangle(32, y, 336, 22);
            return label;
        }

        private static TextBox MakeTextBox(int y) => new()
        {
            Bounds = new Rectangle(32, y, 336, 36),
            Font = Ui.MakeFont(11.5F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text
        };

        private async Task TryLoginAsync()
        {
            string email = emailBox.Text.Trim();
            string password = passwordBox.Text;

            if (email.Length == 0 || password.Length == 0)
            {
                ShowError("Add meg az e-mail címed és a jelszavad.");
                return;
            }

            loginButton.Enabled = false;
            loginButton.Text = "Bejelentkezés...";
            errorLabel.Visible = false;

            var (ok, error) = await ApiClient.LoginAsync(email, password);

            if (IsDisposed) return;
            loginButton.Enabled = true;
            loginButton.Text = "Bejelentkezés";

            if (ok)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                passwordBox.Clear();
                passwordBox.Focus();
                ShowError(error ?? "Sikertelen bejelentkezés.");
            }
        }

        private void ShowError(string message)
        {
            errorLabel.Text = message;
            errorLabel.Visible = true;
        }
    }
}
