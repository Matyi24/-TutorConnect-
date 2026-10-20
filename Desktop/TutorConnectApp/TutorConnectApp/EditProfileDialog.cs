using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>
    /// A saját fiók szerkesztése a weboldal mintájára: név, e-mail, jelszó; oktatónak bemutatkozás és
    /// tantárgyak óradíjjal; diáknak iskolatípus, évfolyam és tanulni kívánt tantárgyak.
    /// </summary>
    internal sealed class EditProfileDialog : DialogBase
    {
        private const int MaxSubjects = 10;

        private static readonly string[] SchoolLevels =
            { "", "Általános iskola", "Középiskola", "Egyetem / főiskola", "Felnőtt / egyéb" };

        private readonly AccountDto account;
        private readonly bool isTutor;
        private readonly bool isStudent;
        private readonly TextBox nameBox;
        private readonly TextBox emailBox;
        private readonly TextBox? bioBox;
        private readonly TextBox? gradeBox;
        private readonly DropdownSelect? levelBox;
        private readonly TextBox newPasswordBox;
        private readonly TextBox currentPasswordBox;
        private readonly FlowLayoutPanel? rateHost;
        private readonly List<(SelectableChip Chip, SubjectDto Subject)> subjectChips = new();
        private readonly Dictionary<int, decimal> rates = new();

        public EditProfileDialog(AccountDto account, List<SubjectDto> subjects)
            : base("Adatok szerkesztése", "Módosítsd a profilodat, majd mentsd el.", "Mentés", Ui.Sz(560, 760), "\uE77B")
        {
            this.account = account;
            isTutor = account.Role == "TUTOR";
            isStudent = account.Role == "STUDENT";

            Section("Alapadatok");
            AddCaption("Név");
            nameBox = NewInput(account.Name, 60);
            AddBody(nameBox);
            AddCaption("E-mail");
            emailBox = NewInput(account.Email, 100);
            AddBody(emailBox);

            if (isTutor)
            {
                Section("Bemutatkozás");
                bioBox = NewTextBox(110, 1000);
                bioBox.Text = account.Bio;
                AddBody(bioBox);

                Section("Tantárgyak és óradíjak");
                AddCaption($"Legfeljebb {MaxSubjects} tantárgyat választhatsz. A kijelölt tantárgyakhoz adhatod meg az óradíjat.");
                foreach (var subject in account.Subjects ?? new List<AccountSubjectDto>())
                    rates[subject.SubjectId] = subject.HourlyRate;

                var selectedIds = rates.Keys.ToHashSet();
                AddSubjectChips(subjects, selectedIds);

                rateHost = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.Transparent
                };
                AddBody(rateHost);
                RebuildRates();
            }

            if (isStudent)
            {
                Section("Tanulmányaid");
                levelBox = new DropdownSelect("Iskolatípus", SchoolLevels.Select(l => l.Length == 0 ? "Nincs megadva" : l),
                    Math.Max(Array.IndexOf(SchoolLevels, account.SchoolLevel ?? ""), 0));
                AddBody(levelBox);
                AddCaption("Osztály / évfolyam");
                gradeBox = NewInput(account.Grade ?? "", 30);
                AddBody(gradeBox);

                Section("Ebben kérnél segítséget");
                AddCaption($"Legfeljebb {MaxSubjects} tantárgyat választhatsz.");
                AddSubjectChips(subjects, (account.LearningSubjects ?? new List<NamedDto>()).Select(l => l.SubjectId).ToHashSet());
            }

            Section("Jelszó");
            AddCaption("Új jelszó (nem kötelező). Legalább 6 karakter, legyen benne szám és speciális karakter.");
            newPasswordBox = NewInput("", 100, password: true);
            AddBody(newPasswordBox);
            AddCaption("Jelenlegi jelszó. Az e-mail cím vagy a jelszó módosításához szükséges.");
            currentPasswordBox = NewInput("", 100, password: true);
            AddBody(currentPasswordBox);

            SetFooter("A módosításokat a Mentés gombbal véglegesítheted.");
        }

        private void AddCaption(string text)
        {
            var label = Ui.Lbl(text, 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6));
            label.MaximumSize = new Size(ContentWidth, 0);
            Body.Controls.Add(label);
        }

        /// <summary>Többválasztós tantárgy-pirulák (oktatónál a tanított, diáknál a tanulni kívánt tantárgyak).</summary>
        private void AddSubjectChips(List<SubjectDto> subjects, HashSet<int> selectedIds)
        {
            var flow = NewWrap();
            foreach (var subject in subjects.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var chip = new SelectableChip(subject.Name);
                chip.SetSelected(selectedIds.Contains(subject.Id));
                chip.Click += (_, _) => ToggleSubject(chip, subject);
                flow.Controls.Add(chip);
                subjectChips.Add((chip, subject));
            }
            AddBody(flow);
        }

        private void ToggleSubject(SelectableChip chip, SubjectDto subject)
        {
            if (!chip.IsSelected && subjectChips.Count(c => c.Chip.IsSelected) >= MaxSubjects)
            {
                SetFooter($"Legfeljebb {MaxSubjects} tantárgyat választhatsz.", error: true);
                return;
            }

            chip.SetSelected(!chip.IsSelected);
            SetFooter("A módosításokat a Mentés gombbal véglegesítheted.");
            if (isTutor) RebuildRates();
        }

        /// <summary>Oktatónál minden kijelölt tantárgyhoz egy óradíj-mező tartozik.</summary>
        private void RebuildRates()
        {
            if (rateHost == null) return;
            Ui.Reset(rateHost);

            foreach (var (chip, subject) in subjectChips)
            {
                if (!chip.IsSelected) continue;

                var row = Ui.Row();
                var name = Ui.Lbl(subject.Name, 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 6, 12, 0));
                name.AutoSize = false;
                name.Size = Ui.Sz(220, 28);

                decimal value = rates.TryGetValue(subject.Id, out var known) ? known : 3000;
                rates[subject.Id] = value;

                var input = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100000,
                    Increment = 500,
                    ThousandsSeparator = true,
                    Value = Math.Clamp(value, 0, 100000),
                    Width = Ui.S(120),
                    BackColor = Theme.InputBg,
                    ForeColor = Theme.Text,
                    Font = Ui.MakeFont(10.5F),
                    Margin = Ui.Pad(0, 0, 10, 0)
                };
                int id = subject.Id;
                input.ValueChanged += (_, _) => rates[id] = input.Value;

                row.Controls.Add(name);
                row.Controls.Add(input);
                row.Controls.Add(Ui.Lbl("Ft / óra", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 0, 0)));
                rateHost.Controls.Add(row);
            }
        }

        protected override async Task OnActionAsync()
        {
            string name = nameBox.Text.Trim();
            string email = emailBox.Text.Trim();
            string newPassword = newPasswordBox.Text;
            string currentPassword = currentPasswordBox.Text;

            // ugyanazok a szabályok, mint a szerveren (így azonnal kiderül a hiba)
            if (name.Length < 2 || name.Length > 60)
            { Fail("A név 2 és 60 karakter között legyen."); return; }
            if (!Validation.IsEmail(email))
            { Fail("Adj meg egy érvényes e-mail címet."); return; }
            if (newPassword.Length > 0 && !Validation.IsStrongPassword(newPassword))
            { Fail("Az új jelszó legalább 6 karakter legyen, és tartalmazzon számot és speciális karaktert."); return; }

            bool emailChanged = !string.Equals(email, account.Email, StringComparison.OrdinalIgnoreCase);
            if ((emailChanged || newPassword.Length > 0) && currentPassword.Length == 0)
            { Fail("E-mail vagy jelszó módosításához add meg a jelenlegi jelszavad."); return; }

            var payload = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["email"] = email,
                ["currentPassword"] = currentPassword,
                ["newPassword"] = newPassword
            };

            if (isTutor)
            {
                payload["bio"] = bioBox?.Text.Trim() ?? "";
                payload["subjects"] = subjectChips
                    .Where(c => c.Chip.IsSelected)
                    .Select(c => new Dictionary<string, object?>
                    {
                        ["subject_id"] = c.Subject.Id,
                        ["hourly_rate"] = (int)(rates.TryGetValue(c.Subject.Id, out var rate) ? rate : 0)
                    })
                    .ToList();
            }

            if (isStudent)
            {
                payload["school_level"] = SchoolLevels[Math.Max(levelBox?.SelectedIndex ?? 0, 0)];
                payload["grade"] = gradeBox?.Text.Trim() ?? "";
                payload["learning_subjects"] = subjectChips.Where(c => c.Chip.IsSelected).Select(c => c.Subject.Id).ToList();
            }

            ActionButton.Enabled = false;
            try
            {
                await ApiClient.SaveAccountAsync(payload);
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

        private void Fail(string message) => SetFooter(message, error: true);
    }
}
