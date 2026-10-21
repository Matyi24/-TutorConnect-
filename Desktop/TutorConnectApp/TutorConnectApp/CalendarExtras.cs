using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json;

namespace TutorConnectApp
{
    /// <summary>
    /// Az oktató szabadságai (nem tanít ezeken a napokon). A szerver nem ismeri a fogalmat, ezért helyben,
    /// felhasználónként tárolódik (%AppData%\TutorConnect\timeoff-azonosító.json). A naptár szürkével jelöli,
    /// és figyelmeztet, ha ilyen napra adsz meg időpontot.
    /// </summary>
    internal static class TimeOffStore
    {
        /// <summary>Egy szabadság: első és utolsó nap (mindkettő beleszámít).</summary>
        public sealed class Range
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        private static List<Range> ranges = new();
        private static int loadedFor = -1;

        private static string FilePath(int userId) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TutorConnect", $"timeoff-{userId}.json");

        private static void EnsureLoaded()
        {
            int userId = ApiClient.CurrentUser?.Id ?? -1;
            if (userId == loadedFor) return;

            loadedFor = userId;
            ranges = new List<Range>();
            if (userId < 0) return;

            try
            {
                string path = FilePath(userId);
                if (File.Exists(path))
                    ranges = JsonSerializer.Deserialize<List<Range>>(File.ReadAllText(path)) ?? new List<Range>();

                // a már lejárt szabadságokat nem tartjuk meg
                ranges.RemoveAll(r => r.End.Date < DateTime.Today);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                ranges = new List<Range>();
            }
        }

        private static void Save()
        {
            if (loadedFor < 0) return;

            try
            {
                string path = FilePath(loadedFor);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(ranges));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a mentés nem kritikus
            }
        }

        public static IReadOnlyList<Range> Ranges
        {
            get
            {
                EnsureLoaded();
                return ranges.OrderBy(r => r.Start).ToList();
            }
        }

        public static bool IsBlocked(DateTime day)
        {
            EnsureLoaded();
            var date = day.Date;
            return ranges.Any(r => date >= r.Start.Date && date <= r.End.Date);
        }

        public static void Add(DateTime start, DateTime end)
        {
            EnsureLoaded();
            ranges.Add(new Range { Start = start.Date, End = end.Date });
            Save();
        }

        public static void Remove(Range range)
        {
            EnsureLoaded();
            ranges.Remove(range);
            Save();
        }
    }

    /// <summary>Szabadság megadása és a meglévők kezelése az oktatónak.</summary>
    internal sealed class TimeOffDialog : DialogBase
    {
        private const int DaysAhead = 180;

        private readonly ComboBox fromBox;
        private readonly ComboBox toBox;
        private readonly CheckBox clearBox;
        private readonly FlowLayoutPanel listHost;

        public string ResultMessage { get; private set; } = "";

        public TimeOffDialog()
            : base("Szabadság", "Jelöld meg azokat a napokat, amikor nem tanítasz.", "Szabadság rögzítése", Ui.Sz(560, 640), "\uE787")
        {
            Section("1   Mettől meddig?");
            var days = new List<DateTime>();
            for (int i = 0; i < DaysAhead; i++) days.Add(DateTime.Today.AddDays(i));

            fromBox = NewCombo(250);
            toBox = NewCombo(250);
            foreach (var day in days)
            {
                string text = day.ToString("yyyy. MMM d. (ddd)", Ui.Hu);
                fromBox.Items.Add(text);
                toBox.Items.Add(text);
            }
            fromBox.SelectedIndex = 1;
            toBox.SelectedIndex = 1;
            fromBox.SelectedIndexChanged += (_, _) =>
            {
                if (toBox.SelectedIndex < fromBox.SelectedIndex) toBox.SelectedIndex = fromBox.SelectedIndex;
            };

            var pickers = Ui.Row();
            pickers.Controls.Add(Ui.Lbl("Első nap", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 8, 0)));
            pickers.Controls.Add(fromBox);
            AddBody(pickers);

            var pickersEnd = Ui.Row();
            pickersEnd.Controls.Add(Ui.Lbl("Utolsó nap", 10F, Theme.Muted, margin: Ui.Pad(0, 6, 8, 0)));
            pickersEnd.Controls.Add(toBox);
            AddBody(pickersEnd);

            clearBox = new CheckBox
            {
                Text = "A már meghirdetett, még le nem foglalt időpontok törlése ebben az időszakban",
                Checked = true,
                AutoSize = true,
                MaximumSize = new Size(ContentWidth, 0),
                ForeColor = Theme.Text,
                BackColor = Theme.PageBg,
                Font = Ui.MakeFont(10F),
                Margin = Ui.Pad(0, 4, 0, 4)
            };
            AddBody(clearBox);

            Section("Megadott szabadságaid");
            listHost = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            AddBody(listHost);
            RenderList();

            SetFooter("A szabadság csak a te naptáradban jelenik meg szürkén: a diákok a szabad időpontjaidat látják, azokat törölheted.");
        }

        private void RenderList()
        {
            listHost.SuspendLayout();
            Ui.Reset(listHost);

            var ranges = TimeOffStore.Ranges;
            if (ranges.Count == 0)
            {
                listHost.Controls.Add(Ui.Lbl("Még nincs megadott szabadságod.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            }
            else
            {
                foreach (var range in ranges)
                {
                    var row = Ui.Row();
                    string text = range.Start.Date == range.End.Date
                        ? range.Start.ToString("yyyy. MMMM d.", Ui.Hu)
                        : $"{range.Start.ToString("yyyy. MMMM d.", Ui.Hu)} – {range.End.ToString("MMMM d.", Ui.Hu)}";
                    row.Controls.Add(Ui.Lbl(text, 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 8, 16, 0)));
                    var captured = range;
                    row.Controls.Add(Ui.Btn("Törlés", Theme.CardBg, Theme.Danger, (_, _) =>
                    {
                        TimeOffStore.Remove(captured);
                        BeginInvoke(new Action(RenderList));
                    }, outline: true));
                    listHost.Controls.Add(row);
                }
            }
            listHost.ResumeLayout(true);
        }

        protected override async Task OnActionAsync()
        {
            var from = DateTime.Today.AddDays(fromBox.SelectedIndex);
            var to = DateTime.Today.AddDays(toBox.SelectedIndex);
            if (to < from)
            {
                SetFooter("Az utolsó nap nem lehet korábbi az elsőnél.", error: true);
                return;
            }

            ActionButton.Enabled = false;
            int deleted = 0;
            try
            {
                if (clearBox.Checked)
                {
                    SetFooter("A meghirdetett időpontok törlése...");
                    var slots = await ApiClient.GetAsync<List<SlotItemDto>>("/api/availability");
                    foreach (var slot in slots.Where(s => !s.IsBooked))
                    {
                        var day = slot.StartTime.ToLocalTime().Date;
                        if (day < from || day > to) continue;

                        await ApiClient.DeleteAsync($"/api/availability/{slot.Id}");
                        deleted++;
                        if (IsDisposed) return;
                    }
                }

                TimeOffStore.Add(from, to);
                ResultMessage = deleted > 0
                    ? $"A szabadság rögzítve, {deleted} szabad időpont törölve."
                    : "A szabadság rögzítve.";
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

    // ================================================================
    // Havi nézet
    // ================================================================

    /// <summary>
    /// Havi áttekintő: minden napon színes jelzések a szabad időpontokról és a foglalásokról.
    /// Egy napra kattintva a naptár az adott hét heti nézetére vált (DayClicked).
    /// </summary>
    internal sealed class MonthCalendar : Control
    {
        private static readonly string[] DayNames = { "Hétfő", "Kedd", "Szerda", "Csütörtök", "Péntek", "Szombat", "Vasárnap" };
        private static readonly string[] ShortNames = { "H", "K", "Sze", "Cs", "P", "Szo", "V" };

        private DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private Dictionary<DateTime, List<CalEvent>> byDay = new();
        private int hoverCell = -1;

        public event Action<DateTime>? DayClicked;

        public MonthCalendar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleName = "Havi naptár";
            AccessibleRole = AccessibleRole.Table;
            Margin = Ui.Pad(0, 0, 0, 16);
            Cursor = Cursors.Hand;
            UpdateHeight();
        }

        public DateTime Month => month;

        public void SetMonth(DateTime anyDay)
        {
            month = new DateTime(anyDay.Year, anyDay.Month, 1);
            UpdateHeight();
            Invalidate();
        }

        public void SetEvents(IEnumerable<CalEvent> items)
        {
            // a napi csoportosítás itt készül el egyszer, nem minden újrafestéskor (egérmozgatásnál is rajzol)
            byDay = items
                .GroupBy(ev => ev.Start.Date)
                .ToDictionary(group => group.Key, group => group.ToList());
            Invalidate();
        }

        private static int HeaderHeight => Ui.S(44);
        private static int RowHeight => Ui.S(104);

        private DateTime GridStart => WeekCalendar.StartOfWeek(month);

        private int Rows
        {
            get
            {
                var last = month.AddMonths(1).AddDays(-1);
                int weeks = (int)((WeekCalendar.StartOfWeek(last) - GridStart).TotalDays / 7) + 1;
                return Math.Clamp(weeks, 4, 6);
            }
        }

        private void UpdateHeight() => Height = HeaderHeight + Rows * RowHeight + Ui.S(8);

        private Rectangle CellRect(int index)
        {
            double cellWidth = (Width - 2) / 7.0;
            int column = index % 7;
            int row = index / 7;
            return new Rectangle((int)(1 + column * cellWidth), HeaderHeight + row * RowHeight, (int)cellWidth, RowHeight);
        }

        private int CellAt(Point point)
        {
            if (point.Y < HeaderHeight || point.X < 1 || point.X >= Width - 1) return -1;
            int column = Math.Min((int)((point.X - 1) / ((Width - 2) / 7.0)), 6);
            int row = (point.Y - HeaderHeight) / RowHeight;
            int index = row * 7 + column;
            return row >= 0 && row < Rows ? index : -1;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int cell = CellAt(e.Location);
            if (cell == hoverCell) return;
            hoverCell = cell;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverCell = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int cell = CellAt(e.Location);
            if (cell >= 0 && e.Button == MouseButtons.Left)
                DayClicked?.Invoke(GridStart.AddDays(cell));
        }

        private static (string Label, Color Color) KindStyle(CalKind kind, int count) => kind switch
        {
            CalKind.Free => ($"{count} szabad", Theme.Success),
            CalKind.Pending => ($"{count} függő", Theme.Warning),
            CalKind.Confirmed => ($"{count} elfogadott", Theme.MainBlue),
            CalKind.Completed => ($"{count} lezajlott", Theme.Muted),
            _ => ($"{count} lefoglalt", Color.FromArgb(0x84, 0x5E, 0xF7))
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Ui.S(16)))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Theme.CardBorder);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            double cellWidth = (Width - 2) / 7.0;
            bool narrow = cellWidth < Ui.S(110);
            var headerFont = Ui.MakeFont(9F, FontStyle.Bold);
            for (int day = 0; day < 7; day++)
            {
                var rect = new Rectangle((int)(1 + day * cellWidth), 0, (int)cellWidth, HeaderHeight);
                TextRenderer.DrawText(g, narrow ? ShortNames[day] : DayNames[day], headerFont, rect, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            using var line = new Pen(Theme.CardBorder);
            int rows = Rows;
            g.DrawLine(line, 1, HeaderHeight, Width - 2, HeaderHeight);
            for (int row = 1; row < rows; row++)
                g.DrawLine(line, 1, HeaderHeight + row * RowHeight, Width - 2, HeaderHeight + row * RowHeight);
            for (int column = 1; column < 7; column++)
            {
                int x = (int)(1 + column * cellWidth);
                g.DrawLine(line, x, HeaderHeight, x, HeaderHeight + rows * RowHeight);
            }

            var today = DateTime.Today;
            for (int index = 0; index < rows * 7; index++)
            {
                var date = GridStart.AddDays(index);
                var rect = CellRect(index);
                bool inMonth = date.Month == month.Month;

                if (index == hoverCell)
                {
                    using var hoverBrush = new SolidBrush(Color.FromArgb(60, Theme.SoftHover));
                    g.FillRectangle(hoverBrush, Rectangle.Inflate(rect, -1, -1));
                }

                if (TimeOffStore.IsBlocked(date))
                {
                    using var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(50, Theme.Muted), Color.Transparent);
                    g.FillRectangle(hatch, Rectangle.Inflate(rect, -1, -1));
                }

                // a nap száma (ma: kék körben)
                var numberRect = new Rectangle(rect.X + Ui.S(8), rect.Y + Ui.S(6), Ui.S(30), Ui.S(26));
                if (date == today)
                {
                    using var pill = Ui.RoundedPath(numberRect, Ui.S(13));
                    using var accent = new SolidBrush(Theme.MainBlue);
                    g.FillPath(accent, pill);
                }
                TextRenderer.DrawText(g, date.Day.ToString(), Ui.MakeFont(10.5F, FontStyle.Bold), numberRect,
                    date == today ? Theme.White : inMonth ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                if (!byDay.TryGetValue(date, out var dayEvents)) continue;

                int y = rect.Y + Ui.S(38);
                int shown = 0;
                foreach (var kindGroup in dayEvents.GroupBy(ev => ev.Kind).OrderBy(group => (int)group.Key))
                {
                    if (shown == 3 || y + Ui.S(18) > rect.Bottom) break;

                    var (label, color) = KindStyle(kindGroup.Key, kindGroup.Count());
                    var pillRect = new Rectangle(rect.X + Ui.S(6), y, rect.Width - Ui.S(12), Ui.S(18));
                    using (var pillPath = Ui.RoundedPath(pillRect, Ui.S(8)))
                    {
                        using var fill = new SolidBrush(Anim.Lerp(Theme.CardBg, color, inMonth ? (Theme.Dark ? 0.32 : 0.18) : 0.08));
                        g.FillPath(fill, pillPath);
                    }
                    TextRenderer.DrawText(g, label, Ui.MakeFont(8F, FontStyle.Bold), Rectangle.Inflate(pillRect, -Ui.S(6), 0),
                        inMonth ? color : Theme.Muted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    y += Ui.S(21);
                    shown++;
                }
            }
        }
    }
}
