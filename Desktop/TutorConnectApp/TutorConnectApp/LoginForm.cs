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
        private readonly CheckBox rememberBox;
        private readonly int slideOffset = 0;

        public LoginForm()
        {
            Text = "TutorConnect – Bejelentkezés";
            StartPosition = FormStartPosition.CenterScreen;
            // a kezdő méret ne nőjön túl a képernyőn nagy nagyításnál sem
            var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            ClientSize = new Size(Math.Min(Ui.S(620), workArea.Width - Ui.S(40)), Math.Min(Ui.S(740), workArea.Height - Ui.S(80)));
            MinimumSize = Ui.Sz(480, 700);
            Font = Ui.MakeFont(10F);
            DoubleBuffered = true;
            ResizeRedraw = true;
            Opacity = 0; // az OnShown-ban úszik be

            card = new SurfacePanel { Size = Ui.Sz(400, 612) };

            var logo = new AvatarCircle("Tutor Connect", 68) { Location = Ui.Pt((400 - 68) / 2, 32) };

            var title = Ui.Lbl("TutorConnect", 21F, Theme.Text, bold: true);
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Bounds = Ui.Rc(0, 110, 400, 40);

            var subtitle = Ui.Lbl("Jelentkezz be a folytatáshoz", 10.5F, Theme.Muted);
            subtitle.AutoSize = false;
            subtitle.TextAlign = ContentAlignment.MiddleCenter;
            subtitle.Bounds = Ui.Rc(0, 150, 400, 26);

            var emailLabel = MakeLabel("E-mail", 192);
            emailBox = MakeTextBox(216);

            var passwordLabel = MakeLabel("Jelszó", 266);
            passwordBox = MakeTextBox(290);
            passwordBox.UseSystemPasswordChar = true;

            rememberBox = new CheckBox
            {
                Text = "Emlékezz rám",
                Bounds = Ui.Rc(32, 336, 170, 28),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Font = Ui.MakeFont(9.5F),
                Cursor = Cursors.Hand
            };

            var forgotLink = Ui.Lbl("Elfelejtett jelszó?", 9.5F, Theme.MainBlue, bold: true);
            forgotLink.AutoSize = false;
            forgotLink.TextAlign = ContentAlignment.MiddleRight;
            forgotLink.Bounds = Ui.Rc(206, 336, 162, 28);
            forgotLink.Cursor = Cursors.Hand;
            forgotLink.Click += (_, _) => OpenForgotPassword();

            errorLabel = Ui.Lbl("", 9.5F, Theme.Danger);
            errorLabel.AutoSize = false;
            errorLabel.Bounds = Ui.Rc(32, 372, 336, 44);
            errorLabel.Visible = false;

            loginButton = Ui.PrimaryBtn("Bejelentkezés", async (_, _) => await TryLoginAsync());
            loginButton.AutoSize = false;
            loginButton.Font = Ui.MakeFont(11F, FontStyle.Bold);
            loginButton.Bounds = Ui.Rc(32, 424, 336, 50);
            loginButton.Margin = Ui.Pad(0);

            var registerButton = Ui.Btn("Fiók létrehozása", Theme.CardBg, Theme.Text, (_, _) => OpenRegister(), outline: true);
            registerButton.AutoSize = false;
            registerButton.Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            registerButton.Bounds = Ui.Rc(32, 486, 336, 46);
            registerButton.Margin = Ui.Pad(0);

            var hint = Ui.Lbl($"Szerver: {ApiClient.BaseUrl}", 8.5F, Theme.Muted);
            hint.AutoSize = false;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            hint.Bounds = Ui.Rc(0, 558, 400, 24);

            card.Controls.AddRange(new Control[] { logo, title, subtitle, emailLabel, emailBox, passwordLabel, passwordBox, rememberBox, forgotLink, errorLabel, loginButton, registerButton, hint });
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
            Anim.Run(250, t => Opacity = t); // csak áttűnés: a kártya mozgatása újrarajzolta volna az egész háttérátmenetet
            emailBox.Focus();
        }

        private static Label MakeLabel(string text, int y)
        {
            var label = Ui.Lbl(text, 10F, Theme.Muted);
            label.AutoSize = false;
            label.Bounds = Ui.Rc(32, y, 336, 22);
            return label;
        }

        private static TextBox MakeTextBox(int y) => new()
        {
            Bounds = Ui.Rc(32, y, 336, 36),
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

            var (ok, error) = await ApiClient.LoginAsync(email, password, rememberBox.Checked);

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

        private void OpenForgotPassword()
        {
            using var dialog = new ForgotPasswordDialog(emailBox.Text.Trim());
            dialog.ShowDialog(this);
        }

        private void OpenRegister()
        {
            using var dialog = new RegisterDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (dialog.LoggedIn)
            {
                // a szerver a regisztrációval be is léptetett
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            emailBox.Text = dialog.Email;
            passwordBox.Clear();
            passwordBox.Focus();
            ShowMessage("A fiókod elkészült. Jelentkezz be az e-mail címeddel és a jelszavaddal.", error: false);
        }

        private void ShowError(string message) => ShowMessage(message, error: true);

        private void ShowMessage(string message, bool error)
        {
            errorLabel.ForeColor = error ? Theme.Danger : Theme.Success;
            errorLabel.Text = message;
            errorLabel.Visible = true;
        }
    }
}
