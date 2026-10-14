using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>
    /// A dialógusok közös váza: fejléc (cím, alcím), görgethető tartalom, lábléc (állapotsor + művelet-gomb).
    /// Megjelenéskor halványan beúszik, a keret fekete, a színek a témát követik.
    /// </summary>
    internal abstract class DialogBase : Form
    {
        protected const int ContentWidth = 488;

        protected readonly FlowLayoutPanel Body;
        protected readonly HoverButton ActionButton;
        private readonly Label footerLabel;

        protected DialogBase(string title, string subtitle, string actionText, Size clientSize)
        {
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

            var header = new Panel { Dock = DockStyle.Top, Height = 88, BackColor = Theme.CardBg };
            var titleLabel = Ui.Lbl(title, 17F, Theme.Text, bold: true);
            titleLabel.Location = new Point(24, 16);
            var subtitleLabel = Ui.Lbl(subtitle, 10.5F, Theme.Muted);
            subtitleLabel.Location = new Point(26, 52);
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            Body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(24, 0, 12, 12),
                BackColor = Theme.CardBg
            };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 92, BackColor = Theme.CardBg };
            footer.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            const int buttonWidth = 190;
            footerLabel = new Label
            {
                Bounds = new Rectangle(24, 12, clientSize.Width - 24 - buttonWidth - 36, 68),
                ForeColor = Theme.Muted,
                BackColor = Theme.CardBg
            };

            ActionButton = Ui.PrimaryBtn(actionText, async (_, _) => await OnActionAsync());
            ActionButton.AutoSize = false;
            ActionButton.Bounds = new Rectangle(clientSize.Width - 24 - buttonWidth, 24, buttonWidth, 46);
            ActionButton.Margin = new Padding(0);

            footer.Controls.Add(footerLabel);
            footer.Controls.Add(ActionButton);

            Controls.Add(Body);   // a kitöltő elem legyen elöl a sorrendben
            Controls.Add(header);
            Controls.Add(footer);
        }

        /// <summary>A művelet-gombra kattintáskor fut le.</summary>
        protected abstract Task OnActionAsync();

        protected void Section(string text) =>
            Body.Controls.Add(Ui.Lbl(text, 11F, Theme.Text, bold: true, margin: new Padding(0, 14, 0, 8)));

        protected void AddBody(Control control)
        {
            control.Margin = new Padding(0, 0, 0, 8);
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
            Margin = new Padding(0, 0, 0, 6)
        };

        protected static TextBox NewTextBox(int height, int maxLength) => new()
        {
            Width = ContentWidth,
            Height = height,
            Multiline = true,
            MaxLength = maxLength,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(10.5F)
        };

        protected static ComboBox NewCombo(int width) => new()
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(10.5F),
            Margin = new Padding(0, 0, 16, 0)
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
            : base("Időpontfoglalás", SubtitleFor(tutor), "Foglalás elküldése", new Size(560, 700))
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
                    slotsHost.Controls.Add(Ui.Lbl(Ui.FormatDay(day.First().StartTime), 10F, Theme.Muted, bold: true, margin: new Padding(0, 6, 0, 6)));

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
            : base("Óra értékelése", $"{booking.SubjectName ?? "Óra"}  ·  {booking.OtherName ?? "oktató"}", "Értékelés mentése", new Size(540, 500))
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

        public AvailabilityDialog()
            : base("Új időpont megadása", "Add meg, mikor tudsz tanítani. A diákok ezek közül foglalhatnak.", "Mentés", new Size(560, 700))
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
            timing.Margin = new Padding(0, 0, 0, 8);
            timeBox = NewCombo(150);
            for (int minutes = 6 * 60; minutes <= 22 * 60; minutes += 30)
                timeBox.Items.Add($"{minutes / 60:00}:{minutes % 60:00}");
            timeBox.SelectedItem = "16:00";

            durationBox = NewCombo(150);
            foreach (int duration in Durations)
                durationBox.Items.Add($"{duration} perc");
            durationBox.SelectedIndex = 2; // 60 perc

            timing.Controls.Add(Ui.Lbl("Kezdés", 10F, Theme.Muted, margin: new Padding(0, 6, 8, 0)));
            timing.Controls.Add(timeBox);
            timing.Controls.Add(Ui.Lbl("Időtartam", 10F, Theme.Muted, margin: new Padding(0, 6, 8, 0)));
            timing.Controls.Add(durationBox);
            AddBody(timing);

            Section("3   Ismétlés");
            var repeat = Ui.Row();
            repeatBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 12,
                Value = 1,
                Width = 80,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                Font = Ui.MakeFont(10.5F),
                Margin = new Padding(0, 0, 12, 0)
            };
            repeatHint = Ui.Lbl("Egyszeri időpont", 10F, Theme.Muted, margin: new Padding(0, 6, 0, 0));
            repeatBox.ValueChanged += (_, _) => UpdateRepeatHint();
            repeat.Controls.Add(repeatBox);
            repeat.Controls.Add(repeatHint);
            AddBody(repeat);

            ActionButton.Enabled = false;
            SetFooter("Válaszd ki a napot.");
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
}
