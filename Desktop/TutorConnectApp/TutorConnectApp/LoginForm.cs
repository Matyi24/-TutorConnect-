namespace TutorConnectApp
{
    /// <summary>
    /// Bejelentkező ablak. Az app csak sikeres belépés (DialogResult.OK) után nyitja meg a főablakot.
    /// Kódból épül fel (nincs Designer), az ablak átméretezhető, a kártya mindig középen marad.
    /// </summary>
    public class LoginForm : Form
    {
        private readonly TextBox emailBox;
        private readonly TextBox passwordBox;
        private readonly Button loginButton;
        private readonly Label errorLabel;

        public LoginForm()
        {
            Text = "TutorConnect – Bejelentkezés";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 640);
            MinimumSize = new Size(460, 600);
            BackColor = Theme.MainBlueLight;
            Font = new Font(Theme.FontFamily, 10F);

            var card = new Panel
            {
                Size = new Size(380, 480),
                BackColor = Theme.White,
                Anchor = AnchorStyles.None // átméretezésnél középen marad
            };
            card.Location = new Point((ClientSize.Width - card.Width) / 2, (ClientSize.Height - card.Height) / 2);

            var title = new Label
            {
                Text = "TutorConnect",
                Font = new Font(Theme.FontFamily, 22F, FontStyle.Bold),
                ForeColor = Theme.MainBlueDark,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 32, 380, 48)
            };
            var subtitle = new Label
            {
                Text = "Jelentkezz be a folytatáshoz",
                ForeColor = Theme.MainGrey,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 80, 380, 28)
            };

            var emailLabel = MakeLabel("E-mail", 130);
            emailBox = MakeTextBox(156);

            var passwordLabel = MakeLabel("Jelszó", 210);
            passwordBox = MakeTextBox(236);
            passwordBox.UseSystemPasswordChar = true;

            errorLabel = new Label
            {
                ForeColor = Color.FromArgb(0xC9, 0x2A, 0x2A),
                AutoSize = false,
                Bounds = new Rectangle(32, 290, 316, 56),
                Visible = false
            };

            loginButton = new Button
            {
                Text = "Bejelentkezés",
                Bounds = new Rectangle(32, 360, 316, 48),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.MainBlue,
                ForeColor = Theme.White,
                Font = new Font(Theme.FontFamily, 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            loginButton.FlatAppearance.BorderSize = 0;
            loginButton.FlatAppearance.MouseOverBackColor = Theme.MainBlueDark;
            loginButton.Click += async (_, _) => await TryLoginAsync();

            var hint = new Label
            {
                Text = $"Szerver: {ApiClient.BaseUrl}",
                ForeColor = Theme.MainGrey,
                Font = new Font(Theme.FontFamily, 8.5F),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Bounds = new Rectangle(0, 436, 380, 24)
            };

            card.Controls.AddRange(new Control[] { title, subtitle, emailLabel, emailBox, passwordLabel, passwordBox, errorLabel, loginButton, hint });
            Controls.Add(card);

            AcceptButton = loginButton; // Enter = bejelentkezés
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        private static Label MakeLabel(string text, int y) => new()
        {
            Text = text,
            ForeColor = Theme.DarkText,
            AutoSize = false,
            Bounds = new Rectangle(32, y, 316, 24)
        };

        private static TextBox MakeTextBox(int y) => new()
        {
            Bounds = new Rectangle(32, y, 316, 32),
            Font = new Font(Theme.FontFamily, 11F),
            BorderStyle = BorderStyle.FixedSingle
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
