namespace TutorConnectApp
{
    /// <summary>Időpontfoglalás egy oktatónál (tantárgy + szabad időpont + üzenet).</summary>
    internal class BookDialog : Form
    {
        private sealed record Choice(int Id, string Text)
        {
            public override string ToString() => Text;
        }

        private readonly TutorDto tutor;
        private readonly ComboBox subjectBox;
        private readonly ComboBox slotBox;
        private readonly TextBox noteBox;
        private readonly Button bookButton;
        private readonly Label statusLabel;

        public BookDialog(TutorDto tutor)
        {
            this.tutor = tutor;

            Text = $"Időpontfoglalás – {tutor.FullName}";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 440);
            BackColor = Theme.White;
            Font = Ui.MakeFont(10F);

            Controls.Add(Ui.Lbl(tutor.FullName, 15F, Theme.DarkText, bold: true));
            Controls[0].Location = new Point(24, 20);

            subjectBox = MakeCombo(92);
            slotBox = MakeCombo(160);
            noteBox = new TextBox
            {
                Bounds = new Rectangle(24, 228, 412, 80),
                Multiline = true,
                MaxLength = 300,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle
            };

            statusLabel = new Label
            {
                Bounds = new Rectangle(24, 318, 412, 44),
                ForeColor = Ui.Danger
            };

            bookButton = Ui.Btn("Foglalás elküldése", Theme.MainBlue, Theme.White, async (_, _) => await BookAsync());
            bookButton.AutoSize = false;
            bookButton.Bounds = new Rectangle(24, 376, 412, 44);
            bookButton.Margin = new Padding(0);
            bookButton.Enabled = false;

            AddLabel("Tantárgy", 68);
            AddLabel("Szabad időpont", 136);
            AddLabel("Üzenet az oktatónak (nem kötelező)", 204);
            Controls.AddRange(new Control[] { subjectBox, slotBox, noteBox, statusLabel, bookButton });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                var data = await ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{tutor.Id}/availability");
                if (IsDisposed) return;

                foreach (var subject in data.Subjects)
                    subjectBox.Items.Add(new Choice(subject.Id, subject.HourlyRate > 0 ? $"{subject.Name} – {Ui.Money(subject.HourlyRate)}/óra" : subject.Name));
                foreach (var slot in data.Slots)
                    slotBox.Items.Add(new Choice(slot.Id, Ui.FormatRange(slot.StartTime, slot.EndTime)));

                if (subjectBox.Items.Count > 0) subjectBox.SelectedIndex = 0;
                if (slotBox.Items.Count > 0) slotBox.SelectedIndex = 0;

                if (data.Slots.Count == 0)
                    ShowStatus("Ennél az oktatónál jelenleg nincs szabad időpont.", Ui.Muted);
                else if (data.Subjects.Count == 0)
                    ShowStatus("Az oktató még nem adott meg tantárgyat.", Ui.Muted);
                else
                    bookButton.Enabled = true;
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) ShowStatus(ex.Message, Ui.Danger);
            }
        }

        private async Task BookAsync()
        {
            if (subjectBox.SelectedItem is not Choice subject || slotBox.SelectedItem is not Choice slot)
            {
                ShowStatus("Válassz tantárgyat és időpontot.", Ui.Danger);
                return;
            }

            bookButton.Enabled = false;
            ShowStatus("", Ui.Danger);

            try
            {
                await ApiClient.PostAsync("/api/bookings", new Dictionary<string, object?>
                {
                    ["availability_id"] = slot.Id,
                    ["subject_id"] = subject.Id,
                    ["note"] = noteBox.Text.Trim()
                });

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                ShowStatus(ex.Message, Ui.Danger);
                bookButton.Enabled = true;
            }
        }

        private void ShowStatus(string text, Color color)
        {
            statusLabel.ForeColor = color;
            statusLabel.Text = text;
        }

        private ComboBox MakeCombo(int y) => new()
        {
            Bounds = new Rectangle(24, y, 412, 32),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Ui.MakeFont(10.5F)
        };

        private void AddLabel(string text, int y)
        {
            var label = Ui.Lbl(text, 9.5F, Ui.Muted);
            label.Location = new Point(24, y);
            Controls.Add(label);
        }
    }
}
