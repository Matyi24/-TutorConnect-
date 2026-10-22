namespace TutorConnectApp
{
    /// <summary>Rövid indoklás bekérése (felfüggesztés, értékelés törlése): a szöveget a szerver naplózza.</summary>
    internal sealed class ReasonDialog : DialogBase
    {
        private readonly TextBox box;
        private readonly bool optional;

        public string Reason { get; private set; } = "";

        public ReasonDialog(string title, string subtitle, string label, string placeholder, string confirmText, bool danger, bool optional = false)
            : base(title, subtitle, confirmText, Ui.Sz(560, 420), "")
        {
            this.optional = optional;

            Section(label);
            box = NewTextBox(140, 200);
            box.PlaceholderText = placeholder;
            AddBody(box);

            if (danger)
                ActionButton.SetColors(Theme.Danger, Anim.Lerp(Theme.Danger, Color.Black, 0.14), null);

            SetFooter(optional ? "Az indoklás nem kötelező." : "Az indoklás kötelező (legalább pár szó).");
        }

        protected override Task OnActionAsync()
        {
            string value = box.Text.Trim();
            if (!optional && value.Length < 3)
            {
                SetFooter("Kérlek adj meg legalább pár szót.", error: true);
                return Task.CompletedTask;
            }

            Reason = value;
            DialogResult = DialogResult.OK;
            Close();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Egy jelentés részletei: ki kit jelentett, miért, a jelentéskor mentett üzenetek, és a döntés (intézkedés, elutasítás,
    /// felfüggesztés vagy újranyitás). A webes admin felület jelentés-ablakával azonos tartalmú.
    /// </summary>
    internal sealed class ReportDetailDialog : DialogBase
    {
        private readonly AdminReport report;
        private readonly TextBox? note;
        private readonly List<HoverButton> decisionButtons = new();

        /// <summary>Igaz, ha a jelentés állapota megváltozott (a lista frissítésre szorul).</summary>
        public bool Changed { get; private set; }

        public ReportDetailDialog(AdminReportDetail detail)
            : base($"Jelentés #{detail.Report.Id}", $"{AdminUi.Reason(detail.Report.Reason)}  ·  {AdminUi.ReportStatus(detail.Report.Status).Text}",
                "Bezárás", Ui.Sz(620, 760), "")
        {
            report = detail.Report;
            DismissButton.Visible = false;
            bool isOpen = report.Status == "OPEN";

            // ---------- a jelentés adatai ----------
            Section("A jelentés");
            AddBody(Ui.KeyValue("Jelentő", report.ReporterName ?? "–"));
            AddBody(Ui.KeyValue("Jelentett", $"{report.ReportedName}  ({AdminUi.RoleName(report.ReportedRole)})"));
            AddBody(Ui.KeyValue("Ok", AdminUi.Reason(report.Reason)));
            AddBody(Ui.KeyValue("Beküldve", AdminUi.Date(report.CreatedAt)));
            if (!isOpen)
            {
                AddBody(Ui.KeyValue("Lezárva", AdminUi.Date(report.HandledAt) + (report.HandledByName != null ? "  ·  " + report.HandledByName : "")));
                AddBody(Ui.KeyValue("Admin megjegyzés", string.IsNullOrWhiteSpace(report.AdminNote) ? "nincs" : report.AdminNote!));
            }
            if (!string.IsNullOrWhiteSpace(report.Details))
                AddBody(Notice("", "A jelentő megjegyzése", report.Details!, NoticeBox.Tone.Muted));

            // ---------- a jelentéskor mentett üzenetek ----------
            Section("A jelentéskor mentett üzenetek");
            if (detail.Context.Count == 0)
            {
                AddBody(Ui.Lbl("Nincs mentett üzenet.", 10F, Theme.Muted));
            }
            else
            {
                foreach (var message in detail.Context)
                {
                    bool reported = message.SenderId == report.ReportedUserId;
                    string who = reported ? $"{report.ReportedName} (jelentett)" : $"{report.ReporterName} (jelentő)";
                    string title = $"{who}  ·  {AdminUi.Date(message.CreatedAt)}" + (message.Target ? "  ·  JELENTETT ÜZENET" : "");

                    string body = message.Retracted
                        ? "Az üzenet a jelentés előtt vissza lett vonva."
                        : (message.Content ?? "") + (string.IsNullOrEmpty(message.AttachmentName) ? "" : $"\n📎 {message.AttachmentName}");

                    AddBody(Notice(message.Target ? "" : "", title, body.Length == 0 ? "(üres)" : body,
                        reported ? NoticeBox.Tone.Info : NoticeBox.Tone.Muted));
                }
            }

            // ---------- döntés ----------
            Section(isOpen ? "Döntés" : "Állapot");
            if (isOpen)
            {
                note = NewTextBox(90, 500);
                note.PlaceholderText = "Megjegyzés a döntéshez (nem kötelező)";
                AddBody(note);

                var row = NewWrap();
                row.Controls.Add(DecisionButton("Intézkedés történt", Theme.Success, Theme.White, false,
                    () => ApiClient.PostAsync($"/api/admin/reports/{report.Id}/close", new { outcome = "RESOLVED", note = NoteText() })));
                row.Controls.Add(DecisionButton("Elutasítás (nem megalapozott)", Theme.CardBg, Theme.Text, true,
                    () => ApiClient.PostAsync($"/api/admin/reports/{report.Id}/close", new { outcome = "DISMISSED", note = NoteText() })));
                if (report.ReportedRole != "ADMIN" && report.ReportedSuspended == null)
                    row.Controls.Add(DecisionButton("Felhasználó felfüggesztése", Theme.CardBg, Theme.Danger, true, SuspendAsync));
                AddBody(row);
            }
            else
            {
                var row = NewWrap();
                row.Controls.Add(DecisionButton("Újranyitás", Theme.CardBg, Theme.Text, true,
                    () => ApiClient.PostAsync($"/api/admin/reports/{report.Id}/reopen")));
                AddBody(row);
            }

            SetFooter("");
        }

        private static NoticeBox Notice(string glyph, string title, string text, NoticeBox.Tone tone) =>
            new(glyph, title, text, tone) { Width = ContentWidth, Tag = null };

        private string NoteText() => note?.Text.Trim() ?? "";

        private HoverButton DecisionButton(string text, Color back, Color fore, bool outline, Func<Task> action)
        {
            var button = Ui.Btn(text, back, fore, null, outline);
            button.Click += async (_, _) => await RunAsync(button, action);
            decisionButtons.Add(button);
            return button;
        }

        private async Task RunAsync(HoverButton button, Func<Task> action)
        {
            foreach (var b in decisionButtons) b.Enabled = false;
            SetFooter("");

            try
            {
                await action();
                Changed = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                foreach (var b in decisionButtons) b.Enabled = true; // a felfüggesztés indoklás-ablakát bezárták
            }
            catch (ApiException ex)
            {
                SetFooter(ex.Message, error: true);
                foreach (var b in decisionButtons) b.Enabled = true;
            }
        }

        private async Task SuspendAsync()
        {
            using var dialog = new ReasonDialog($"{report.ReportedName} felfüggesztése", "A felfüggesztett felhasználó nem tud belépni.",
                "Indoklás (ezt a felhasználó belépéskor látni fogja)", "pl. Zaklató üzenetek más felhasználók felé", "Felfüggesztés", danger: true);
            if (dialog.ShowDialog(this) != DialogResult.OK) throw new OperationCanceledException(); // nem történt semmi

            await ApiClient.PostAsync($"/api/admin/users/{report.ReportedUserId}/suspend", new { reason = dialog.Reason });

            // a jelentést is lezárjuk: az intézkedés megtörtént
            string text = ("Felfüggesztve: " + dialog.Reason);
            await ApiClient.PostAsync($"/api/admin/reports/{report.Id}/close", new { outcome = "RESOLVED", note = text.Length > 500 ? text[..500] : text });
        }

        protected override Task OnActionAsync()
        {
            DialogResult = Changed ? DialogResult.OK : DialogResult.Cancel;
            Close();
            return Task.CompletedTask;
        }
    }
}
