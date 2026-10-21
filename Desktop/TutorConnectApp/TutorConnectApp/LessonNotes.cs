using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// Saját jegyzetek az órákhoz (pl. "ezt kell átnézni", "házi feladat"). Felhasználónként külön fájlban tárolódnak a gépen
    /// (%AppData%\TutorConnect\notes-azonosító.json), a szerver és a másik fél nem látja őket.
    /// </summary>
    internal static class LessonNotes
    {
        public const int MaxLength = 2000;

        private static Dictionary<string, string> notes = new();
        private static int loadedFor = -1;

        private static string FilePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", $"notes-{userId}.json");

        private static void EnsureLoaded()
        {
            int userId = ApiClient.CurrentUser?.Id ?? -1;
            if (userId == loadedFor) return;

            loadedFor = userId;
            notes = new Dictionary<string, string>();
            if (userId < 0) return;

            try
            {
                string path = FilePath(userId);
                if (File.Exists(path))
                    notes = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                notes = new Dictionary<string, string>(); // sérült vagy nem olvasható fájl: üres jegyzetek
            }
        }

        /// <summary>Az óra jegyzete; üres szöveg, ha nincs.</summary>
        public static string Get(int bookingId)
        {
            EnsureLoaded();
            return notes.TryGetValue(bookingId.ToString(), out var text) ? text : "";
        }

        /// <summary>Elmenti a jegyzetet; üres szöveg törli.</summary>
        public static void Set(int bookingId, string text)
        {
            EnsureLoaded();
            if (loadedFor < 0) return;

            text = text.Trim();
            if (text.Length > MaxLength) text = text[..MaxLength];

            if (text.Length == 0) notes.Remove(bookingId.ToString());
            else notes[bookingId.ToString()] = text;

            try
            {
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(notes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a mentés nem kritikus
            }
        }

        /// <summary>Rövid, egysoros kivonat a kártyához ("sor1 sor2…").</summary>
        public static string Preview(string text, int max = 160)
        {
            string flat = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
        }
    }

    /// <summary>Jegyzet szerkesztése egy órához.</summary>
    internal sealed class NoteDialog : DialogBase
    {
        private readonly BookingDto booking;
        private readonly TextBox box;

        public NoteDialog(BookingDto booking)
            : base("Jegyzet az órához", $"{booking.SubjectName ?? "Óra"}  ·  {booking.OtherName ?? ""}  ·  {Ui.FormatDay(booking.StartTime)}", "Mentés", Ui.Sz(560, 440), "\uE70F")
        {
            this.booking = booking;

            Section("Jegyzetem");
            box = NewTextBox(180, LessonNotes.MaxLength);
            box.Text = LessonNotes.Get(booking.Id).Replace("\r\n", "\n").Replace("\n", "\r\n");
            AddBody(box);

            SetFooter("Csak te látod, a gépeden tárolódik. Üresen mentve a jegyzet törlődik.");
        }

        protected override Task OnActionAsync()
        {
            LessonNotes.Set(booking.Id, box.Text.Replace("\r\n", "\n"));
            DialogResult = DialogResult.OK;
            Close();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A jegyzet sora egy óra kártyáján: a jegyzet kivonata és egy gomb a szerkesztéshez. Mentés után maga frissül,
    /// nincs szükség az oldal újratöltésére.
    /// </summary>
    internal sealed class LessonNoteRow : FlowLayoutPanel
    {
        private readonly BookingDto booking;
        private readonly Label preview;
        private readonly HoverButton button;

        public LessonNoteRow(BookingDto booking)
        {
            this.booking = booking;

            FlowDirection = FlowDirection.LeftToRight;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.Transparent;
            Margin = Ui.Pad(0, 0, 0, 4);
            Padding = Ui.Pad(0);

            button = Ui.Btn("", Theme.CardBg, Theme.MainBlue, (_, _) => Edit(), outline: true);
            button.Margin = Ui.Pad(0, 0, 12, 0);

            preview = Ui.Lbl("", 10F, Theme.Muted, margin: Ui.Pad(0, 9, 0, 0));
            preview.MaximumSize = new Size(Ui.S(460), 0);

            Controls.Add(button);
            Controls.Add(preview);
            UpdateView();
        }

        private void UpdateView()
        {
            string note = LessonNotes.Get(booking.Id);
            button.Text = note.Length == 0 ? "Jegyzet hozzáadása" : "Jegyzet szerkesztése";
            button.AccessibleName = $"{button.Text}: {booking.SubjectName}";
            preview.Text = LessonNotes.Preview(note);
            preview.Visible = note.Length > 0;
        }

        private void Edit()
        {
            using var dialog = new NoteDialog(booking);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                UpdateView();
        }
    }
}
