using System.Globalization;
using System.Text.RegularExpressions;

namespace TutorConnectApp
{
    /// <summary>
    /// A dialógusok közös váza: színátmenetes fejléc (ikon, cím, alcím), görgethető tartalom,
    /// lábléc (állapotsor + Mégse + művelet-gomb). Megjelenéskor halványan beúszik, a keret fekete,
    /// a színek a témát követik. A megadott méretnél kicsit nagyobb ablak nyílik (a képernyőre korlátozva).
    /// </summary>
    internal abstract class DialogBase : Form
    {
        /// <summary>A tartalom (pirulák, beviteli mezők) szélessége; a képernyő nagyításával együtt nő.</summary>
        protected static int ContentWidth => Ui.S(528);

        private readonly FlowLayoutPanel root;
        private SectionGroup? group;

        /// <summary>A tartalom helye: az aktuális szakasz doboza (a Section hívás után), korábban maga a görgetett felület.</summary>
        protected FlowLayoutPanel Body => group ?? root;

        protected readonly HoverButton ActionButton;
        protected readonly HoverButton DismissButton;
        private readonly Label footerLabel;

        protected DialogBase(string title, string subtitle, string actionText, Size clientSize, string glyph = "")
        {
            // nagyobb ablak a korábbinál, de nem lóg ki a képernyőről
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            clientSize = new Size(
                Math.Min(clientSize.Width + Ui.S(80), area.Width - Ui.S(60)),
                Math.Min(clientSize.Height + Ui.S(70), area.Height - Ui.S(110)));

            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = clientSize;
            BackColor = Theme.CardBg;
            ForeColor = Theme.Text;
            Font = Ui.MakeFont(10F);
            Opacity = 0; // az OnShown-ban úszik be

            var header = new DialogHeader(title, subtitle, glyph, clientSize.Width) { Dock = DockStyle.Top };

            // sima görgetésű tartalom; a szakaszok dobozai (SectionGroup) ennek a panelnek a gyerekei
            var scroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.CardBg };
            root = scroll.Content;
            root.Padding = Ui.Pad(28, 18, 12, 14);

            int footerHeight = Ui.S(104);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = footerHeight, BackColor = Theme.PageBg };
            footer.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            int buttonHeight = Ui.S(48);
            int buttonY = (footerHeight - buttonHeight) / 2;
            int actionWidth = Ui.S(214);
            int dismissWidth = Ui.S(120);
            int right = clientSize.Width - Ui.S(28);

            ActionButton = Ui.PrimaryBtn(actionText, async (_, _) => await OnActionAsync());
            ActionButton.AutoSize = false;
            ActionButton.Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            ActionButton.Bounds = new Rectangle(right - actionWidth, buttonY, actionWidth, buttonHeight);
            ActionButton.Margin = Ui.Pad(0);

            DismissButton = Ui.Btn("Mégse", Theme.CardBg, Theme.Text, null, outline: true);
            DismissButton.AutoSize = false;
            DismissButton.Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            DismissButton.Bounds = new Rectangle(ActionButton.Left - Ui.S(12) - dismissWidth, buttonY, dismissWidth, buttonHeight);
            DismissButton.Margin = Ui.Pad(0);
            DismissButton.DialogResult = DialogResult.Cancel; // Esc és a gomb is bezárja
            CancelButton = DismissButton;

            footerLabel = new Label
            {
                Bounds = new Rectangle(Ui.S(28), Ui.S(12), Math.Max(DismissButton.Left - Ui.S(16) - Ui.S(28), Ui.S(80)), footerHeight - Ui.S(24)),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            footer.Controls.Add(footerLabel);
            footer.Controls.Add(DismissButton);
            footer.Controls.Add(ActionButton);

            Controls.Add(scroll); // a kitöltő elem legyen elöl a sorrendben
            Controls.Add(header);
            Controls.Add(footer);
        }

        /// <summary>A művelet-gombra kattintáskor fut le.</summary>
        protected abstract Task OnActionAsync();

        /// <summary>
        /// Új szakasz: külön doboz címmel és elválasztó vonallal; a következő AddBody / Body.Controls.Add hívások
        /// ebbe kerülnek a következő Section-ig. Az "1   Szöveg" alakú címeknél a szám kék körben jelenik meg.
        /// </summary>
        protected void Section(string text)
        {
            group = new SectionGroup(ContentWidth + Ui.S(40));
            root.Controls.Add(group);
            group.Controls.Add(new SectionHeading(text, ContentWidth));
        }

        protected void AddBody(Control control)
        {
            control.Margin = Ui.Pad(0, 0, 0, 10);
            Body.Controls.Add(control);
        }

        protected void SetFooter(string text, bool error = false)
        {
            footerLabel.ForeColor = error ? Theme.Danger : Theme.Muted;
            footerLabel.Text = text;
        }

        /// <summary>Tördelődő, automatikus magasságú sor a tartalom szélességére korlátozva.</summary>
        protected static FlowLayoutPanel NewWrap() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(ContentWidth, 0),
            BackColor = Color.Transparent,
            Margin = Ui.Pad(0, 0, 0, 6)
        };

        protected static TextBox NewInput(string text, int maxLength, bool password = false) => new()
        {
            Width = ContentWidth,
            MaxLength = maxLength,
            Text = text,
            UseSystemPasswordChar = password,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F)
        };

        protected static TextBox NewTextBox(int height, int maxLength) => new()
        {
            Width = ContentWidth,
            Height = Ui.S(height),
            Multiline = true,
            MaxLength = maxLength,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F)
        };

        protected static ComboBox NewCombo(int width) => new()
        {
            Width = Ui.S(width),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F),
            Margin = Ui.Pad(0, 0, 16, 0)
        };

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Anim.Run(220, t => Opacity = t);
        }
    }

    /// <summary>Időpontfoglalás egy oktatónál: tantárgy → szabad időpont (naponként csoportosítva) → üzenet.</summary>
    internal sealed class BookDialog : DialogBase
    {
        private readonly TutorDto tutor;
        private readonly FlowLayoutPanel subjectFlow;
        private readonly FlowLayoutPanel slotsHost;
        private readonly TextBox noteBox;
        private readonly List<(SelectableChip Chip, SlotSubjectDto Subject)> subjectChips = new();
        private readonly List<(SelectableChip Chip, SlotDto Slot)> slotChips = new();
        private SlotSubjectDto? selectedSubject;
        private SlotDto? selectedSlot;

        public BookDialog(TutorDto tutor)
            : base("Időpontfoglalás", SubtitleFor(tutor), "Foglalás elküldése", Ui.Sz(560, 700), "\uE787")
        {
            this.tutor = tutor;

            Section("1   Válassz tantárgyat");
            subjectFlow = NewWrap();
            subjectFlow.Controls.Add(Ui.Loading());
            AddBody(subjectFlow);

            Section("2   Válassz időpontot");
            slotsHost = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            slotsHost.Controls.Add(Ui.Loading());
            AddBody(slotsHost);

            Section("3   Üzenet az oktatónak (nem kötelező)");
            noteBox = NewTextBox(76, 300);
            AddBody(noteBox);

            ActionButton.Enabled = false;
            SetFooter("Válassz tantárgyat és időpontot.");
        }

        private static string SubtitleFor(TutorDto tutor) =>
            tutor.ReviewCount > 0
                ? $"{tutor.FullName}  ·  ★ {tutor.AverageRating.ToString("0.0", Ui.Hu)} ({tutor.ReviewCount} értékelés)"
                : tutor.FullName;

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await LoadAvailabilityAsync();
        }

        private async Task LoadAvailabilityAsync()
        {
            try
            {
                var data = await ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{tutor.Id}/availability");
                if (IsDisposed) return;

                Ui.Reset(subjectFlow);
                Ui.Reset(slotsHost);

                if (data.Subjects.Count == 0)
                    subjectFlow.Controls.Add(Ui.Lbl("Az oktató még nem adott meg tantárgyat.", 10F, Theme.Muted));

                foreach (var subject in data.Subjects)
                {
                    string text = subject.HourlyRate > 0 ? $"{subject.Name} · {Ui.Money(subject.HourlyRate)}/óra" : subject.Name;
                    var chip = new SelectableChip(text);
                    chip.Click += (_, _) => SelectSubject(subject);
                    subjectFlow.Controls.Add(chip);
                    subjectChips.Add((chip, subject));
                }

                if (data.Slots.Count == 0)
                    slotsHost.Controls.Add(Ui.Lbl("Ennél az oktatónál jelenleg nincs szabad időpont.", 10F, Theme.Muted));

                foreach (var day in data.Slots.OrderBy(s => s.StartTime).GroupBy(s => s.StartTime.ToLocalTime().Date))
                {
                    slotsHost.Controls.Add(Ui.Lbl(Ui.FormatDay(day.First().StartTime), 10F, Theme.Muted, bold: true, margin: Ui.Pad(0, 6, 0, 6)));

                    var flow = NewWrap();
                    foreach (var slot in day)
                    {
                        var chip = new SelectableChip(Ui.FormatTimeRange(slot.StartTime, slot.EndTime));
                        chip.Click += (_, _) => SelectSlot(slot);
                        flow.Controls.Add(chip);
                        slotChips.Add((chip, slot));
                    }
                    slotsHost.Controls.Add(flow);
                }

                if (data.Subjects.Count == 1)
                    SelectSubject(data.Subjects[0]); // egyetlen tantárgynál nem kell választani
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                Ui.Reset(subjectFlow);
                Ui.Reset(slotsHost);
                SetFooter(ex.Message, error: true);
            }
        }

        private void SelectSubject(SlotSubjectDto subject)
        {
            selectedSubject = subject;
            foreach (var (chip, item) in subjectChips)
                chip.SetSelected(item == subject);
            UpdateSummary();
        }

        private void SelectSlot(SlotDto slot)
        {
            selectedSlot = slot;
            foreach (var (chip, item) in slotChips)
                chip.SetSelected(item == slot);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (selectedSubject == null || selectedSlot == null)
            {
                ActionButton.Enabled = false;
                SetFooter(selectedSubject == null ? "Válassz tantárgyat." : "Válassz időpontot.");
                return;
            }

            string price = selectedSubject.HourlyRate > 0 ? $"\nÓradíj: {Ui.Money(selectedSubject.HourlyRate)}" : "";
            SetFooter($"{selectedSubject.Name}\n{Ui.FormatRange(selectedSlot.StartTime, selectedSlot.EndTime)}{price}");
            ActionButton.Enabled = true;
        }

        protected override async Task OnActionAsync()
        {
            if (selectedSubject == null || selectedSlot == null) return;

            ActionButton.Enabled = false;
            try
            {
                await ApiClient.PostAsync("/api/bookings", new Dictionary<string, object?>
                {
                    ["availability_id"] = selectedSlot.Id,
                    ["subject_id"] = selectedSubject.Id,
                    ["note"] = noteBox.Text.Trim()
                });

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

    /// <summary>Lezajlott óra értékelése (1–5 csillag + vélemény).</summary>
    internal sealed class ReviewDialog : DialogBase
    {
        private readonly BookingDto booking;
        private readonly StarRating stars;
        private readonly TextBox commentBox;

        public ReviewDialog(BookingDto booking)
            : base("Óra értékelése", $"{booking.SubjectName ?? "Óra"}  ·  {booking.OtherName ?? "oktató"}", "Értékelés mentése", Ui.Sz(560, 500), "\uE734")
        {
            this.booking = booking;

            Section("Hány csillagot adsz?");
            stars = new StarRating();
            AddBody(stars);

            Section("Vélemény (nem kötelező)");
            commentBox = NewTextBox(130, 1000);
            AddBody(commentBox);

            SetFooter("Az értékelés az oktató profilján is megjelenik.");
        }

        protected override async Task OnActionAsync()
        {
            if (stars.Value < 1)
            {
                SetFooter("Válassz 1 és 5 csillag között.", error: true);
                return;
            }

            ActionButton.Enabled = false;
            try
            {
                await ApiClient.PostAsync($"/api/bookings/{booking.Id}/review", new Dictionary<string, object?>
                {
                    ["rating"] = stars.Value,
                    ["comment"] = commentBox.Text.Trim()
                });

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

    /// <summary>Oktatói időpont megadása: nap → kezdés + időtartam → ismétlés (hetente).</summary>
    internal sealed class AvailabilityDialog : DialogBase
    {
        private static readonly int[] Durations = { 30, 45, 60, 90, 120 };

        private readonly List<(SelectableChip Chip, DateTime Day)> dayChips = new();
        private readonly ComboBox timeBox;
        private readonly ComboBox durationBox;
        private readonly NumericUpDown repeatBox;
        private readonly Label repeatHint;
        private DateTime? selectedDay;

        public string ResultMessage { get; private set; } = "";

        public AvailabilityDialog(DateTime? presetStart = null, int? presetMinutes = null)
            : base("Új időpont megadása", "Add meg, mikor tudsz tanítani. A diákok ezek közül foglalhatnak.", "Mentés", Ui.Sz(560, 700), "\uE121")
        {
            Section("1   Melyik napon?");
            var days = NewWrap();
            for (int i = 0; i < 21; i++)
            {
                DateTime day = DateTime.Today.AddDays(i);
                var chip = new SelectableChip(day.ToString("MMM d. · ddd", Ui.Hu));
                chip.Click += (_, _) => SelectDay(day);
                days.Controls.Add(chip);
                dayChips.Add((chip, day));
            }
            AddBody(days);

            Section("2   Kezdés és időtartam");
            var timing = Ui.Row();
            timing.Margin = Ui.Pad(0, 0, 0, 8);
            timeBox = NewCombo(150);
            for (int minutes = 6 * 60; minutes <= 22 * 60; minutes += 30)
                timeBox.Items.Add($"{minutes / 60:00}:{minutes % 60:00}");
            timeBox.SelectedItem = "16:00";

            durationBox = NewCombo(150);
            foreach (int duration in Durations)
                durationBox.Items.Add($"{duration} perc");
            durationBox.SelectedIndex = 2; // 60 perc

            timing.Controls.Add(Ui.Lbl("Kezdés", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 8, 0)));
            timing.Controls.Add(timeBox);
            timing.Controls.Add(Ui.Lbl("Időtartam", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 8, 0)));
            timing.Controls.Add(durationBox);
            AddBody(timing);

            Section("3   Ismétlés");
            var repeat = Ui.Row();
            repeatBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 12,
                Value = 1,
                Width = Ui.S(80),
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Font = Ui.MakeFont(10.5F),
                Margin = Ui.Pad(0, 0, 12, 0)
            };
            repeatHint = Ui.Lbl("Egyszeri időpont", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 0, 0));
            repeatBox.ValueChanged += (_, _) => UpdateRepeatHint();
            repeat.Controls.Add(repeatBox);
            repeat.Controls.Add(repeatHint);
            AddBody(repeat);

            ActionButton.Enabled = false;
            SetFooter("Válaszd ki a napot.");

            // a naptárból húzott idősáv előre kitölti a mezőket
            if (presetStart is DateTime preset)
            {
                var match = dayChips.FirstOrDefault(c => c.Day == preset.Date);
                if (match.Chip != null) SelectDay(match.Day);

                string time = $"{preset.Hour:00}:{preset.Minute / 30 * 30:00}";
                if (timeBox.Items.Contains(time)) timeBox.SelectedItem = time;
            }
            if (presetMinutes is int length)
            {
                // a legnagyobb megengedett időtartam, ami még belefér a kijelölésbe
                int index = Array.FindLastIndex(Durations, d => d <= length);
                durationBox.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        private void SelectDay(DateTime day)
        {
            selectedDay = day;
            foreach (var (chip, item) in dayChips)
                chip.SetSelected(item == day);
            ActionButton.Enabled = true;
            SetFooter($"{Ui.FormatDay(day)}  ·  {timeBox.SelectedItem}");
        }

        private void UpdateRepeatHint()
        {
            int weeks = (int)repeatBox.Value;
            repeatHint.Text = weeks == 1 ? "Egyszeri időpont" : $"Hetente ismétlődik, összesen {weeks} alkalom";
        }

        protected override async Task OnActionAsync()
        {
            if (selectedDay is not DateTime day) return;

            string time = timeBox.SelectedItem?.ToString() ?? "16:00";
            int minutes = Durations[Math.Max(durationBox.SelectedIndex, 0)];

            ActionButton.Enabled = false;
            try
            {
                var result = await ApiClient.PostAsync<AvailabilityResultDto>("/api/availability", new Dictionary<string, object?>
                {
                    ["start"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T" + time,
                    ["minutes"] = minutes,
                    ["repeat_weeks"] = (int)repeatBox.Value
                });

                ResultMessage = result.Created == 1 ? "1 időpont létrejött." : $"{result.Created} időpont létrejött.";
                if (result.Skipped > 0)
                    ResultMessage += $" {result.Skipped} kimaradt (ütközés vagy túl messze van).";

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

    /// <summary>
    /// A saját fiók szerkesztése a weboldal mintájára: név, e-mail, jelszó; oktatónak bemutatkozás és
    /// tantárgyak óradíjjal; diáknak iskolatípus, évfolyam és tanulni kívánt tantárgyak.
    /// </summary>
    internal sealed class EditProfileDialog : DialogBase
    {
        private const int MaxSubjects = 10;

        private static readonly string[] SchoolLevels =
            { "", "Általános iskola", "Középiskola", "Egyetem / főiskola", "Felnőtt / egyéb" };

        private static readonly Regex EmailPattern = new(@"^[^\s@]+@[^\s@]+\.[^\s@]+$");
        private static readonly Regex StrongPassword = new(@"^(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{6,}$");

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
            if (!EmailPattern.IsMatch(email) || email.Length > 100)
            { Fail("Adj meg egy érvényes e-mail címet."); return; }
            if (newPassword.Length > 0 && !StrongPassword.IsMatch(newPassword))
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

