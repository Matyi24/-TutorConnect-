using System.Globalization;

namespace TutorConnectApp
{
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
}
