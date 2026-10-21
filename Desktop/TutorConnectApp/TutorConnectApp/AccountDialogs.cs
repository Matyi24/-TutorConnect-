using System.ComponentModel;
using System.Diagnostics;

namespace TutorConnectApp
{
    /// <summary>A szerver weboldalának megnyitása az alapértelmezett böngészőben (adatvédelmi tájékoztató, feltételek).</summary>
    internal static class Web
    {
        public static void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ApiClient.BaseUrl + path) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                MessageBox.Show("Nem sikerült megnyitni a böngészőt.\nCím: " + ApiClient.BaseUrl + path, "TutorConnect",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    /// <summary>Kattintható, linknek kinéző felirat.</summary>
    internal static class LinkLabels
    {
        public static Label Create(string text, Action onClick, float size = 9.5F)
        {
            var label = Ui.Lbl(text, size, Theme.MainBlue, bold: true);
            label.Cursor = Cursors.Hand;
            label.Click += (_, _) => onClick();
            return label;
        }
    }

    /// <summary>Új fiók létrehozása (diák vagy oktató). A szerver siker esetén be is léptet.</summary>
    internal sealed class RegisterDialog : DialogBase
    {

        private readonly TextBox nameBox;
        private readonly TextBox emailBox;
        private readonly TextBox passwordBox;
        private readonly TextBox confirmBox;
        private readonly DropdownSelect roleBox;
        private readonly CheckBox acceptBox;

        /// <summary>Igaz, ha a regisztráció után a szerver be is léptetett.</summary>
        public bool LoggedIn { get; private set; }

        public string Email { get; private set; } = "";

        public RegisterDialog()
            : base("Fiók létrehozása", "Add meg az adataidat, és már használhatod is az appot.", "Regisztráció", Ui.Sz(560, 760), "\uE77B")
        {
            Section("Személyes adatok");
            Caption("Név");
            nameBox = NewInput("", 60);
            AddBody(nameBox);

            Caption("E-mail");
            emailBox = NewInput("", 100);
            AddBody(emailBox);

            Section("Jelszó");
            Caption("Jelszó (legalább 6 karakter, számmal és speciális karakterrel)");
            passwordBox = NewInput("", 100, password: true);
            AddBody(passwordBox);

            Caption("Jelszó újra");
            confirmBox = NewInput("", 100, password: true);
            AddBody(confirmBox);

            Section("Szerepkör");
            Caption("Ki vagy te?");
            roleBox = new DropdownSelect("Szerepkör", new[] { "Diák vagyok", "Oktató vagyok" });
            roleBox.Margin = Ui.Pad(0, 0, 0, 12);
            AddBody(roleBox);

            Section("Hozzájárulás");
            acceptBox = new CheckBox
            {
                Text = "Elfogadom az adatkezelési tájékoztatót és a felhasználási feltételeket.",
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                ForeColor = Theme.Text,
                BackColor = Theme.PageBg,
                Font = Ui.MakeFont(9.5F),
                Margin = Ui.Pad(0, 4, 0, 6)
            };
            AddBody(acceptBox);

            var links = Ui.Row();
            links.Controls.Add(LinkLabels.Create("Adatvédelmi tájékoztató", () => Web.Open("/adatvedelem")));
            links.Controls.Add(Ui.Lbl("  ·  ", 9.5F, Theme.Muted));
            links.Controls.Add(LinkLabels.Create("Felhasználási feltételek", () => Web.Open("/felhasz_feltetelek")));
            AddBody(links);

            SetFooter("Az e-mail címedet a regisztráció után meg kell erősítened.");
        }

        private void Caption(string text)
        {
            var label = Ui.Lbl(text, 9.5F, Theme.Muted, margin: Ui.Pad(0, 6, 0, 6));
            label.MaximumSize = new Size(ContentWidth, 0);
            Body.Controls.Add(label);
        }

        protected override async Task OnActionAsync()
        {
            string name = nameBox.Text.Trim();
            string email = emailBox.Text.Trim();
            string password = passwordBox.Text;

            // ugyanazok a szabályok, mint a szerveren (így azonnal kiderül a hiba)
            if (name.Length < 2 || name.Length > 60)
            { SetFooter("A név 2 és 60 karakter között legyen.", error: true); return; }
            if (!Validation.IsEmail(email))
            { SetFooter("Adj meg egy érvényes e-mail címet.", error: true); return; }
            if (!Validation.IsStrongPassword(password))
            { SetFooter(Validation.PasswordRule, error: true); return; }
            if (password != confirmBox.Text)
            { SetFooter("A két jelszó nem egyezik.", error: true); return; }
            if (!acceptBox.Checked)
            { SetFooter("A folytatáshoz el kell fogadnod a tájékoztatót és a feltételeket.", error: true); return; }

            ActionButton.Enabled = false;
            SetFooter("Fiók létrehozása...");

            string role = roleBox.SelectedIndex == 1 ? "TUTOR" : "STUDENT";
            var (ok, loggedIn, error) = await ApiClient.RegisterAsync(name, email, password, role);
            if (IsDisposed) return;

            if (ok)
            {
                LoggedIn = loggedIn;
                Email = email;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            SetFooter(error ?? "A regisztráció nem sikerült.", error: true);
            ActionButton.Enabled = true;
        }
    }

    /// <summary>Jelszó-visszaállító levél kérése. A levélben lévő linket a böngészőben kell megnyitni.</summary>
    internal sealed class ForgotPasswordDialog : DialogBase
    {

        private readonly TextBox emailBox;
        private bool sent;

        public ForgotPasswordDialog(string presetEmail)
            : base("Elfelejtett jelszó", "Küldünk egy levelet egy linkkel, amin új jelszót adhatsz meg.", "Link küldése", Ui.Sz(560, 460), "\uE715")
        {
            Section("E-mail cím");
            var caption = Ui.Lbl("Add meg a fiókodhoz tartozó e-mail címet", 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6));
            Body.Controls.Add(caption);

            emailBox = NewInput(presetEmail, 100);
            AddBody(emailBox);

            Section("Hogyan tovább?");
            var note = Ui.Lbl("A levélben kapott linket a böngészőben nyisd meg: ott adhatod meg az új jelszót, utána itt az appban be tudsz jelentkezni vele.",
                9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 4));
            note.MaximumSize = new Size(ContentWidth, 0);
            Body.Controls.Add(note);

            SetFooter("");
        }

        protected override async Task OnActionAsync()
        {
            if (sent)
            {
                Close();
                return;
            }

            string email = emailBox.Text.Trim();
            if (!Validation.IsEmail(email))
            {
                SetFooter("Adj meg egy érvényes e-mail címet.", error: true);
                return;
            }

            ActionButton.Enabled = false;
            SetFooter("Küldés...");
            try
            {
                string message = await ApiClient.ForgotPasswordAsync(email);
                if (IsDisposed) return;

                sent = true;
                SetFooter(message);
                ActionButton.Text = "Bezárás";
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                SetFooter(ex.Message, error: true);
            }
            ActionButton.Enabled = true;
        }
    }

    /// <summary>A saját fiók végleges törlése: a jelenlegi jelszóval és külön megerősítéssel.</summary>
    internal sealed class DeleteAccountDialog : DialogBase
    {
        private readonly TextBox passwordBox;
        private readonly CheckBox confirmBox;

        public DeleteAccountDialog()
            : base("Fiók törlése", "Ez a művelet nem vonható vissza.", "Fiók végleges törlése", Ui.Sz(560, 520), "\uE74D")
        {
            ActionButton.SetColors(Theme.Danger, Anim.Lerp(Theme.Danger, Color.Black, 0.14), null);

            Section("Mi történik a fiókoddal?");
            var warning = Ui.Lbl("A fiókod adatai anonimizálódnak: a neved, e-mail címed, bemutatkozásod és tantárgyaid törlődnek, a csatolt fájlok is. " +
                                 "A korábbi foglalások és üzenetek a másik fél számára \"Törölt felhasználó\" névvel maradnak meg. Utána kijelentkeztetünk.",
                10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6));
            warning.MaximumSize = new Size(ContentWidth, 0);
            Body.Controls.Add(warning);

            Section("Megerősítés");
            Body.Controls.Add(Ui.Lbl("A jelenlegi jelszavad", 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6)));
            passwordBox = NewInput("", 100, password: true);
            AddBody(passwordBox);

            confirmBox = new CheckBox
            {
                Text = "Értem, hogy a fiók törlése végleges.",
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                ForeColor = Theme.Text,
                BackColor = Theme.PageBg,
                Font = Ui.MakeFont(10F),
                Margin = Ui.Pad(0, 8, 0, 6)
            };
            AddBody(confirmBox);

            SetFooter("");
        }

        protected override async Task OnActionAsync()
        {
            if (passwordBox.Text.Length == 0)
            { SetFooter("A törléshez add meg a jelenlegi jelszavad.", error: true); return; }
            if (!confirmBox.Checked)
            { SetFooter("Jelöld be, hogy megérted: a törlés végleges.", error: true); return; }

            ActionButton.Enabled = false;
            SetFooter("Törlés...");
            try
            {
                await ApiClient.DeleteAccountAsync(passwordBox.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                SetFooter(ex.Message, error: true);
                ActionButton.Enabled = true;
            }
        }
    }
}
