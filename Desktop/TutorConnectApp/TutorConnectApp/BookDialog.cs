using System.Globalization;

namespace TutorConnectApp
{
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
}
