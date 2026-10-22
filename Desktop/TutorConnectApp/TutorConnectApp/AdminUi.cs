namespace TutorConnectApp
{
    /// <summary>Az admin felület közös elemei: szövegek, színek, jelvények, lapozó (a webes admin.js-sel azonos megnevezésekkel).</summary>
    internal static class AdminUi
    {
        public static readonly Dictionary<string, string> Reasons = new()
        {
            ["SPAM"] = "Spam vagy reklám",
            ["HARASSMENT"] = "Zaklatás, sértő viselkedés",
            ["SCAM"] = "Csalás, átverés",
            ["INAPPROPRIATE"] = "Nem megfelelő tartalom",
            ["OTHER"] = "Egyéb"
        };

        private static readonly Dictionary<string, string> Actions = new()
        {
            ["report_resolved"] = "Jelentés lezárva (intézkedés)",
            ["report_dismissed"] = "Jelentés elutasítva",
            ["report_reopened"] = "Jelentés újranyitva",
            ["user_suspended"] = "Felhasználó felfüggesztve",
            ["user_unsuspended"] = "Felfüggesztés feloldva",
            ["user_email_verified"] = "E-mail kézzel megerősítve",
            ["review_deleted"] = "Értékelés törölve"
        };

        public static string Reason(string key) => Reasons.TryGetValue(key, out var text) ? text : key;

        public static string ActionName(string key) => Actions.TryGetValue(key, out var text) ? text : key;

        public static string RoleName(string? role) => role switch
        {
            "STUDENT" => "Diák",
            "TUTOR" => "Oktató",
            "ADMIN" => "Admin",
            _ => role ?? ""
        };

        public static (string Text, string Tone) ReportStatus(string status) => status switch
        {
            "OPEN" => ("Nyitott", "bad"),
            "RESOLVED" => ("Intézkedés történt", "ok"),
            "DISMISSED" => ("Elutasítva", "gray"),
            _ => (status, "gray")
        };

        public static (string Text, string Tone) BookingStatus(string status) => status switch
        {
            "PENDING" => ("Függőben", "warn"),
            "CONFIRMED" => ("Elfogadva", "ok"),
            "REJECTED" => ("Elutasítva", "gray"),
            "CANCELLED" => ("Lemondva", "bad"),
            "COMPLETED" => ("Teljesítve", "ok"),
            _ => (string.IsNullOrEmpty(status) ? "–" : status, "gray")
        };

        public static Color Tone(string tone) => tone switch
        {
            "ok" => Theme.Success,
            "warn" => Theme.Warning,
            "bad" => Theme.Danger,
            "info" => Theme.MainBlue,
            _ => Theme.Muted
        };

        public static Label Badge(string text, string tone) => Ui.Chip(text, Tone(tone));

        public static string Date(DateTime? value) =>
            value == null ? "–" : value.Value.ToLocalTime().ToString("yyyy. MM. dd. HH:mm", Ui.Hu);

        /// <summary>Lapozó: ← oldal / összes · találatok szám →. Egy oldalnál csak a találatok száma látszik.</summary>
        public static Control Pager(int page, int total, int pageSize, Func<int, Task> go)
        {
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(pageSize, 1)));
            var row = Ui.Row();
            row.Margin = Ui.Pad(0, 6, 0, 10);

            if (pages <= 1)
            {
                row.Controls.Add(Ui.Lbl($"{total} találat", 10F, Theme.Muted, margin: Ui.Pad(2, 6, 0, 0)));
                return row;
            }

            var previous = Ui.Btn("←", Theme.CardBg, Theme.Text, async (_, _) => await go(page - 1), outline: true);
            previous.Enabled = page > 1;
            previous.AccessibleName = "Előző oldal";
            var next = Ui.Btn("→", Theme.CardBg, Theme.Text, async (_, _) => await go(page + 1), outline: true);
            next.Enabled = page < pages;
            next.AccessibleName = "Következő oldal";

            row.Controls.Add(previous);
            row.Controls.Add(Ui.Lbl($"{page} / {pages}  ·  {total} találat", 10F, Theme.Muted, margin: Ui.Pad(8, 12, 16, 0)));
            row.Controls.Add(next);
            return row;
        }

        /// <summary>Szűrő-"pirulák" (chip-sor) egyetlen kiválasztottal.</summary>
        public static FlowLayoutPanel ChipRow(IEnumerable<(string Key, string Label)> options, string selected, Func<string, Task> onSelect,
            Dictionary<string, SelectableChip>? store = null)
        {
            var row = Ui.WrapRow();
            foreach (var (key, label) in options)
            {
                string captured = key;
                var chip = new SelectableChip(label);
                chip.SetSelected(key == selected);
                chip.Click += async (_, _) => await onSelect(captured);
                store?.Add(key, chip);
                row.Controls.Add(chip);
            }
            return row;
        }
    }
}
