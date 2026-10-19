using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    internal enum CalKind
    {
        Free,
        Pending,
        Confirmed,
        Completed,
        Booked
    }

    /// <summary>Egy bejegyzés a naptárban: szabad időpont vagy foglalás.</summary>
    internal sealed class CalEvent
    {
        public DateTime Start;
        public DateTime End;
        public CalKind Kind;
        public string Title = "";
        public string Subtitle = "";
        public object? Tag;
    }

    // ================================================================
    // Heti naptár vezérlő
    // ================================================================

    /// <summary>
    /// Heti rács (hétfőtől vasárnapig, órákra osztva). Bejegyzések színes téglalapként; az oktató nézetben
    /// az üres részen egérrel húzva új időpont jelölhető ki (30 perces léptékkel).
    /// </summary>
    internal sealed class WeekCalendar : Control
    {
        private static readonly string[] LongDays = { "Hétfő", "Kedd", "Szerda", "Csütörtök", "Péntek", "Szombat", "Vasárnap" };
        private static readonly string[] ShortDays = { "H", "K", "Sze", "Cs", "P", "Szo", "V" };

        private readonly List<(CalEvent Event, Rectangle Rect)> layout = new();
        private List<CalEvent> events = new();
        private DateTime weekStart = StartOfWeek(DateTime.Today);
        private int firstHour = 7;
        private int lastHour = 21;
        private bool allowSelect;
        private CalEvent? hover;

        private bool selecting;
        private int selectDay;
        private int selectStart;
        private int selectEnd;

        // szabad időpont áthúzása másik időpontra
        private bool allowMove;
        private CalEvent? pressed;
        private Point pressPoint;
        private int grabOffset;
        private bool dragging;
        private DateTime dragTarget;

        public event Action<CalEvent>? EventClicked;
        public event Action<DateTime, DateTime>? RangeSelected;

        /// <summary>Szabad időpontot új helyre húztak: (a bejegyzés, az új kezdés).</summary>
        public event Action<CalEvent, DateTime>? EventMoved;

        /// <summary>Jobb kattintás egy bejegyzésen: (a bejegyzés, a képernyő-pont).</summary>
        public event Action<CalEvent, Point>? EventContext;

        public WeekCalendar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            AccessibleName = "Heti naptár";
            AccessibleRole = AccessibleRole.Table;
            Margin = Ui.Pad(0, 0, 0, 16);
            UpdateHeight();
        }

        public static DateTime StartOfWeek(DateTime day)
        {
            int offset = ((int)day.DayOfWeek + 6) % 7; // hétfő = 0
            return day.Date.AddDays(-offset);
        }

        public DateTime WeekStart => weekStart;

        public void SetAllowSelect(bool value) => allowSelect = value;

        /// <summary>Engedélyezi a szabad időpontok áthúzását (az oktató saját naptárában).</summary>
        public void SetAllowMove(bool value) => allowMove = value;

        public void SetWeek(DateTime anyDayOfWeek)
        {
            weekStart = StartOfWeek(anyDayOfWeek);
            RebuildLayout();
            Invalidate();
        }

        public void SetEvents(IEnumerable<CalEvent> items)
        {
            events = items.ToList();

            // a látható idősáv bővül, ha van korai vagy késői bejegyzés
            firstHour = 7;
            lastHour = 21;
            foreach (var item in events)
            {
                firstHour = Math.Min(firstHour, item.Start.Hour);
                int endHour = item.End.Minute == 0 ? item.End.Hour - 1 : item.End.Hour;
                lastHour = Math.Max(lastHour, Math.Min(endHour, 23));
            }

            UpdateHeight();
            RebuildLayout();
            Invalidate();
        }

        // ------------------------------------------------------------
        // Méretek
        // ------------------------------------------------------------

        private static int TimeColumn => Ui.S(62);
        private static int HeaderHeight => Ui.S(62);
        private static int HourHeight => Ui.S(58);
        private int Hours => lastHour - firstHour + 1;
        private double ColumnWidth => (Width - TimeColumn - Ui.S(8)) / 7.0;

        private void UpdateHeight() => Height = HeaderHeight + Hours * HourHeight + Ui.S(10);

        private int MinuteToY(int minute) => HeaderHeight + (int)((minute - firstHour * 60) / 60.0 * HourHeight);

        private int YToMinute(int y) => firstHour * 60 + (int)((y - HeaderHeight) / (double)HourHeight * 60);

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            RebuildLayout();
        }

        /// <summary>Kiszámolja minden bejegyzés téglalapját; az egymást fedők egymás mellé kerülnek.</summary>
        private void RebuildLayout()
        {
            layout.Clear();
            if (Width < 50) return;

            for (int day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day).Date;
                var dayEvents = events
                    .Where(e => e.Start.Date == date)
                    .OrderBy(e => e.Start)
                    .ThenBy(e => e.End)
                    .ToList();

                var items = new List<(CalEvent Event, int Lane, int Cluster)>();
                var laneEnds = new List<DateTime>();
                var clusterEnd = DateTime.MinValue;
                int cluster = -1;

                foreach (var item in dayEvents)
                {
                    if (item.Start >= clusterEnd)
                    {
                        cluster++;
                        laneEnds.Clear();
                        clusterEnd = item.End;
                    }
                    else if (item.End > clusterEnd)
                    {
                        clusterEnd = item.End;
                    }

                    int lane = laneEnds.FindIndex(t => t <= item.Start);
                    if (lane < 0)
                    {
                        laneEnds.Add(item.End);
                        lane = laneEnds.Count - 1;
                    }
                    else
                    {
                        laneEnds[lane] = item.End;
                    }
                    items.Add((item, lane, cluster));
                }

                double left = TimeColumn + day * ColumnWidth;
                foreach (var (item, lane, group) in items)
                {
                    int lanes = items.Where(i => i.Cluster == group).Max(i => i.Lane) + 1;
                    double laneWidth = (ColumnWidth - Ui.S(6)) / lanes;

                    int startMinute = item.Start.Hour * 60 + item.Start.Minute;
                    int endMinute = startMinute + (int)(item.End - item.Start).TotalMinutes;
                    int top = MinuteToY(startMinute) + Ui.S(1);
                    int height = Math.Max(MinuteToY(endMinute) - MinuteToY(startMinute) - Ui.S(2), Ui.S(24));

                    layout.Add((item, new Rectangle(
                        (int)(left + Ui.S(3) + lane * laneWidth),
                        top,
                        Math.Max((int)laneWidth - Ui.S(2), Ui.S(10)),
                        height)));
                }
            }
        }

        // ------------------------------------------------------------
        // Rajzolás
        // ------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var card = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedPath(card, Ui.S(16)))
            {
                using var fill = new SolidBrush(Theme.CardBg);
                using var border = new Pen(Theme.CardBorder);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            PaintHeader(g);
            PaintGrid(g);
            PaintTimeOff(g);

            foreach (var (item, rect) in layout)
                PaintEvent(g, item, rect, item == hover || (dragging && item == pressed));

            PaintSelection(g);
            PaintDrag(g);
            PaintNowLine(g);
        }

        /// <summary>A szabadságra jelölt napok szürke csíkozással és felirattal.</summary>
        private void PaintTimeOff(Graphics g)
        {
            double colWidth = ColumnWidth;
            int bottom = MinuteToY((lastHour + 1) * 60);

            for (int day = 0; day < 7; day++)
            {
                if (!TimeOffStore.IsBlocked(weekStart.AddDays(day))) continue;

                var rect = new Rectangle((int)(TimeColumn + day * colWidth) + 1, HeaderHeight, (int)colWidth - 1, bottom - HeaderHeight);
                using (var hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(50, Theme.Muted), Color.Transparent))
                    g.FillRectangle(hatch, rect);

                TextRenderer.DrawText(g, "Szabadság", Ui.MakeFont(8.5F, FontStyle.Bold),
                    new Rectangle(rect.X, rect.Y + Ui.S(6), rect.Width, Ui.S(20)), Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }
        }

        /// <summary>Áthúzás közben a célhely halvány kerete a leendő idővel.</summary>
        private void PaintDrag(Graphics g)
        {
            if (!dragging || pressed == null) return;

            int day = (int)(dragTarget.Date - weekStart.Date).TotalDays;
            if (day < 0 || day > 6) return;

            int startMinute = dragTarget.Hour * 60 + dragTarget.Minute;
            int minutes = (int)(pressed.End - pressed.Start).TotalMinutes;
            double colWidth = ColumnWidth;
            var rect = new Rectangle((int)(TimeColumn + day * colWidth) + Ui.S(3), MinuteToY(startMinute),
                (int)colWidth - Ui.S(6), Math.Max(MinuteToY(startMinute + minutes) - MinuteToY(startMinute), Ui.S(24)));

            using var path = Ui.RoundedPath(rect, Ui.S(8));
            using var brush = new SolidBrush(Color.FromArgb(70, Theme.Success));
            using var pen = new Pen(Theme.Success, Ui.S(2F)) { DashStyle = DashStyle.Dash };
            g.FillPath(brush, path);
            g.DrawPath(pen, path);

            string label = $"{startMinute / 60:00}:{startMinute % 60:00} – {(startMinute + minutes) / 60 % 24:00}:{(startMinute + minutes) % 60:00}";
            using var textBrush = new SolidBrush(Theme.Success);
            g.DrawString(label, Ui.MakeFont(9F, FontStyle.Bold), textBrush, new Rectangle(rect.X + Ui.S(6), rect.Y + Ui.S(3), rect.Width, Ui.S(22)));
        }

        private void PaintHeader(Graphics g)
        {
            double colWidth = ColumnWidth;
            bool narrow = colWidth < Ui.S(96);
            var today = DateTime.Today;

            var nameFont = Ui.MakeFont(9F);
            var numberFont = Ui.MakeFont(13F, FontStyle.Bold);
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var mutedBrush = new SolidBrush(Theme.Muted);
            using var textBrush = new SolidBrush(Theme.Text);
            using var accentBrush = new SolidBrush(Theme.MainBlue);
            using var whiteBrush = new SolidBrush(Theme.White);

            for (int day = 0; day < 7; day++)
            {
                var date = weekStart.AddDays(day);
                float x = (float)(TimeColumn + day * colWidth);
                var column = new RectangleF(x, 0, (float)colWidth, HeaderHeight);
                bool isToday = date.Date == today;

                g.DrawString(narrow ? ShortDays[day] : LongDays[day], nameFont, isToday ? accentBrush : mutedBrush,
                    new RectangleF(column.X, Ui.S(8), column.Width, Ui.S(20)), center);

                var numberRect = new Rectangle((int)(column.X + column.Width / 2 - Ui.S(18)), Ui.S(28), Ui.S(36), Ui.S(30));
                if (isToday)
                {
                    using var pill = Ui.RoundedPath(numberRect, Ui.S(15));
                    g.FillPath(accentBrush, pill);
                    g.DrawString(date.Day.ToString(), numberFont, whiteBrush, numberRect, center);
                }
                else
                {
                    g.DrawString(date.Day.ToString(), numberFont, textBrush, numberRect, center);
                }
            }
        }

        private void PaintGrid(Graphics g)
        {
            using var hourLine = new Pen(Theme.CardBorder);
            using var halfLine = new Pen(Color.FromArgb(70, Theme.CardBorder));
            using var labelBrush = new SolidBrush(Theme.Muted);
            using var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            var labelFont = Ui.MakeFont(8.5F);

            int left = TimeColumn;
            int rightEdge = Width - Ui.S(8);

            for (int hour = firstHour; hour <= lastHour + 1; hour++)
            {
                int y = MinuteToY(hour * 60);
                g.DrawLine(hourLine, left, y, rightEdge, y);

                if (hour <= lastHour)
                {
                    g.DrawString($"{hour:00}:00", labelFont, labelBrush,
                        new Rectangle(0, y - Ui.S(10), TimeColumn - Ui.S(10), Ui.S(20)), right);
                    int half = MinuteToY(hour * 60 + 30);
                    g.DrawLine(halfLine, left, half, rightEdge, half);
                }
            }

            // függőleges napelválasztók
            for (int day = 0; day <= 7; day++)
            {
                int x = (int)(TimeColumn + day * ColumnWidth);
                g.DrawLine(hourLine, x, HeaderHeight - Ui.S(2), x, MinuteToY((lastHour + 1) * 60));
            }
        }

        private static (Color Fill, Color Border, Color Text) Colors(CalKind kind)
        {
            Color accent = kind switch
            {
                CalKind.Free => Theme.Success,
                CalKind.Pending => Theme.Warning,
                CalKind.Confirmed => Theme.MainBlue,
                CalKind.Completed => Theme.Muted,
                _ => Color.FromArgb(0x84, 0x5E, 0xF7)
            };

            if (kind == CalKind.Confirmed)
                return (accent, accent, Theme.White);

            Color fill = Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.30 : 0.16);
            return (fill, accent, kind == CalKind.Completed ? Theme.Muted : accent);
        }

        private void PaintEvent(Graphics g, CalEvent item, Rectangle rect, bool hovered)
        {
            var (fill, border, text) = Colors(item.Kind);

            using var path = Ui.RoundedPath(rect, Ui.S(8));
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);
            using (var pen = new Pen(border, hovered ? Ui.S(2F) : 1F))
            {
                if (item.Kind == CalKind.Free) pen.DashStyle = DashStyle.Dash;
                g.DrawPath(pen, path);
            }

            var inner = new Rectangle(rect.X + Ui.S(7), rect.Y + Ui.S(3), rect.Width - Ui.S(12), rect.Height - Ui.S(5));
            if (inner.Width < Ui.S(20)) return;

            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var textBrush = new SolidBrush(text);

            var titleFont = Ui.MakeFont(9F, FontStyle.Bold);
            int titleHeight = Ui.LineHeight(titleFont);
            g.DrawString(item.Title, titleFont, textBrush, new Rectangle(inner.X, inner.Y, inner.Width, titleHeight + 2), format);

            if (item.Subtitle.Length > 0 && inner.Height >= titleHeight * 2)
            {
                var subFont = Ui.MakeFont(8.5F);
                using var subBrush = new SolidBrush(item.Kind == CalKind.Confirmed ? Color.FromArgb(220, Color.White) : Anim.Lerp(text, Theme.Text, 0.4));
                g.DrawString(item.Subtitle, subFont, subBrush,
                    new Rectangle(inner.X, inner.Y + titleHeight, inner.Width, Ui.LineHeight(subFont) + 2), format);
            }
        }

        private void PaintSelection(Graphics g)
        {
            if (!selecting) return;

            double colWidth = ColumnWidth;
            int x = (int)(TimeColumn + selectDay * colWidth) + Ui.S(3);
            var rect = new Rectangle(x, MinuteToY(selectStart), (int)colWidth - Ui.S(6), Math.Max(MinuteToY(selectEnd) - MinuteToY(selectStart), Ui.S(8)));

            using var path = Ui.RoundedPath(rect, Ui.S(8));
            using var brush = new SolidBrush(Color.FromArgb(70, Theme.MainBlue));
            using var pen = new Pen(Theme.MainBlue, Ui.S(2F));
            g.FillPath(brush, path);
            g.DrawPath(pen, path);

            string label = $"{selectStart / 60:00}:{selectStart % 60:00} – {selectEnd / 60:00}:{selectEnd % 60:00}";
            using var textBrush = new SolidBrush(Theme.MainBlue);
            g.DrawString(label, Ui.MakeFont(9F, FontStyle.Bold), textBrush, new Rectangle(rect.X + Ui.S(6), rect.Y + Ui.S(3), rect.Width, Ui.S(22)));
        }

        private void PaintNowLine(Graphics g)
        {
            var now = DateTime.Now;
            int day = (int)(now.Date - weekStart.Date).TotalDays;
            if (day < 0 || day > 6) return;

            int minute = now.Hour * 60 + now.Minute;
            if (minute < firstHour * 60 || minute > (lastHour + 1) * 60) return;

            int y = MinuteToY(minute);
            int x1 = (int)(TimeColumn + day * ColumnWidth);
            int x2 = (int)(TimeColumn + (day + 1) * ColumnWidth);
            using var pen = new Pen(Theme.Danger, Ui.S(2F));
            using var dot = new SolidBrush(Theme.Danger);
            g.DrawLine(pen, x1, y, x2, y);
            g.FillEllipse(dot, x1 - Ui.S(4), y - Ui.S(4), Ui.S(8), Ui.S(8));
        }

        // ------------------------------------------------------------
        // Egér
        // ------------------------------------------------------------

        private CalEvent? EventAt(Point point)
        {
            for (int i = layout.Count - 1; i >= 0; i--)
            {
                if (layout[i].Rect.Contains(point)) return layout[i].Event;
            }
            return null;
        }

        private bool InGrid(Point point) =>
            point.X >= TimeColumn && point.X < Width - Ui.S(8) && point.Y >= HeaderHeight && point.Y < MinuteToY((lastHour + 1) * 60);

        private static int SnapDown(int minute) => minute / 30 * 30;

        private static int SnapUp(int minute) => (minute + 29) / 30 * 30;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // szabad időpont áthúzása
            if (pressed != null && allowMove && pressed.Kind == CalKind.Free && e.Button == MouseButtons.Left)
            {
                if (!dragging && (Math.Abs(e.X - pressPoint.X) > Ui.S(6) || Math.Abs(e.Y - pressPoint.Y) > Ui.S(6)))
                    dragging = true;

                if (dragging)
                {
                    int duration = (int)(pressed.End - pressed.Start).TotalMinutes;
                    int day = Math.Clamp((int)((e.X - TimeColumn) / ColumnWidth), 0, 6);
                    int minute = (int)Math.Round((YToMinute(e.Y) - grabOffset) / 30.0) * 30;
                    minute = Math.Clamp(minute, firstHour * 60, Math.Max((lastHour + 1) * 60 - duration, firstHour * 60));

                    dragTarget = weekStart.AddDays(day).AddMinutes(minute);
                    Cursor = Cursors.SizeAll;
                    Invalidate();
                    return;
                }
            }

            if (selecting)
            {
                int minute = Math.Clamp(YToMinute(e.Y), firstHour * 60, (lastHour + 1) * 60);
                selectEnd = Math.Max(SnapUp(minute), selectStart + 30);
                selectEnd = Math.Min(selectEnd, (lastHour + 1) * 60);
                Invalidate();
                return;
            }

            var item = EventAt(e.Location);
            if (item != hover)
            {
                hover = item;
                Cursor = item != null ? Cursors.Hand : (allowSelect && InGrid(e.Location) ? Cursors.Cross : Cursors.Default);
                Invalidate();
            }
            else if (item == null)
            {
                Cursor = allowSelect && InGrid(e.Location) ? Cursors.Cross : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null && !dragging)
            {
                hover = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                var hit = EventAt(e.Location);
                if (hit != null)
                {
                    // a kattintás vagy az áthúzás eldöntése az egér felengedésekor / mozgatásakor történik
                    pressed = hit;
                    pressPoint = e.Location;
                    dragging = false;
                    grabOffset = YToMinute(e.Y) - (hit.Start.Hour * 60 + hit.Start.Minute);
                    Capture = true;
                    return;
                }
            }

            if (e.Button != MouseButtons.Left || !allowSelect || !InGrid(e.Location)) return;

            int day = Math.Clamp((int)((e.X - TimeColumn) / ColumnWidth), 0, 6);
            int minute = SnapDown(Math.Clamp(YToMinute(e.Y), firstHour * 60, (lastHour + 1) * 60 - 30));

            // múltbeli időpontot nem lehet kijelölni
            var start = weekStart.AddDays(day).AddMinutes(minute);
            if (start.AddMinutes(30) <= DateTime.Now) return;

            selecting = true;
            selectDay = day;
            selectStart = minute;
            selectEnd = minute + 60;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right)
            {
                var item = EventAt(e.Location);
                if (item != null) EventContext?.Invoke(item, PointToScreen(e.Location));
                return;
            }

            if (selecting)
            {
                selecting = false;
                Capture = false;

                var date = weekStart.AddDays(selectDay);
                var start = date.AddMinutes(selectStart);
                var end = date.AddMinutes(selectEnd);
                Invalidate();
                RangeSelected?.Invoke(start, end);
                return;
            }

            if (pressed != null && e.Button == MouseButtons.Left)
            {
                var item = pressed;
                bool wasDragging = dragging;
                var target = dragTarget;
                pressed = null;
                dragging = false;
                Capture = false;
                Cursor = Cursors.Default;
                Invalidate();

                if (wasDragging)
                {
                    if (target != item.Start) EventMoved?.Invoke(item, target);
                    return;
                }

                if (EventAt(e.Location) == item) EventClicked?.Invoke(item);
            }
        }
    }

    // ================================================================
    // Naptár oldal
    // ================================================================

    /// <summary>
    /// Oktatónak: a saját időpontjai és foglalásai heti nézetben; az egérrel végighúzva új időpont adható meg.
    /// Diáknak: az óráik, és egy kiválasztott oktató szabad időpontjai, amelyekre egy kattintással jelentkezhet.
    /// </summary>
    internal sealed class CalendarPage : PageBase
    {
        /// <summary>Az Oktatók oldal ezzel nyitja meg a naptárat egy adott oktatóval.</summary>
        public static int? InitialTutorId;

        private readonly UserInfo me;
        private readonly bool isTutor;
        private List<TutorDto> tutors = new();
        private List<BookingDto> bookings = new();
        private List<SlotItemDto> mySlots = new();
        private AvailabilityDto? tutorAvailability;
        private WeekCalendar calendar = null!;
        private MonthCalendar monthView = null!;
        private SelectableChip weekChip = null!;
        private SelectableChip monthChip = null!;
        private bool monthMode;
        private Label weekLabel = null!;
        private Label infoLabel = null!;
        private DropdownSelect? tutorBox;
        private int refreshId;

        public CalendarPage(UserInfo me) : base("Naptár",
            me.Role == "TUTOR"
                ? "Add meg, mikor tartasz órát: húzd végig az egeret a naptár üres részén. A szabad időpontokat áthúzhatod, jobb gombbal másolhatod vagy törölheted."
                : "Válassz oktatót, majd kattints egy szabad (zöld) időpontra a jelentkezéshez.")
        {
            this.me = me;
            isTutor = me.Role == "TUTOR";
            Realtime.EventReceived += OnRealtime;
        }

        protected override async Task BuildAsync()
        {
            if (!isTutor)
            {
                tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;
            }

            ClearBody();

            // ---------- eszköztár: hét léptetése + (diáknak) oktatóválasztó ----------
            var toolbar = Ui.WrapRow();
            var previous = Ui.Btn("", Theme.CardBg, Theme.Text, (_, _) => Shift(-1), outline: true);
            previous.Font = Ui.IconFont(11F);
            previous.Text = "";
            var today = Ui.Btn("Ma", Theme.CardBg, Theme.Text, (_, _) => GoToday(), outline: true);
            var next = Ui.Btn("", Theme.CardBg, Theme.Text, (_, _) => Shift(1), outline: true);
            next.Font = Ui.IconFont(11F);
            next.Text = "";
            foreach (var button in new[] { previous, today, next })
                button.Margin = Ui.Pad(0, 0, 8, 12);

            weekLabel = Ui.Lbl("", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(8, 6, 24, 0));
            toolbar.Controls.Add(previous);
            toolbar.Controls.Add(today);
            toolbar.Controls.Add(next);
            toolbar.Controls.Add(weekLabel);

            // nézetváltó: heti vagy havi áttekintés
            monthMode = false;
            weekChip = new SelectableChip("Hét");
            monthChip = new SelectableChip("Hónap");
            weekChip.SetSelected(true);
            weekChip.Click += (_, _) => SetViewMode(false);
            monthChip.Click += (_, _) => SetViewMode(true);
            toolbar.Controls.Add(weekChip);
            toolbar.Controls.Add(monthChip);

            var export = Ui.Btn("Exportálás (.ics)", Theme.CardBg, Theme.Text, async (_, _) => await ExportAsync(), outline: true);
            export.Margin = Ui.Pad(8, 0, 8, 12);
            toolbar.Controls.Add(export);

            if (isTutor)
            {
                var copyWeek = Ui.Btn("Hét másolása →", Theme.CardBg, Theme.Text, async (_, _) => await CopyWeekAsync(), outline: true);
                copyWeek.Margin = Ui.Pad(0, 0, 8, 12);
                toolbar.Controls.Add(copyWeek);

                var timeOff = Ui.Btn("Szabadság", Theme.CardBg, Theme.Text, async (_, _) => await OpenTimeOffAsync(), outline: true);
                timeOff.Margin = Ui.Pad(0, 0, 16, 12);
                toolbar.Controls.Add(timeOff);
            }

            if (!isTutor)
            {
                int initial = 0;
                if (InitialTutorId is int id)
                {
                    int index = tutors.FindIndex(t => t.Id == id);
                    if (index >= 0) initial = index + 1;
                    InitialTutorId = null;
                }

                var names = new List<string> { "Válassz oktatót" };
                names.AddRange(tutors.Select(t => t.FullName));
                tutorBox = new DropdownSelect("Oktató", names, initial);
                tutorBox.Margin = Ui.Pad(0, 0, 0, 12);
                tutorBox.SelectionChanged += async (_, _) => await RefreshDataAsync();
                toolbar.Controls.Add(tutorBox);
            }
            AddRow(toolbar);

            infoLabel = Ui.Lbl("", 9.5F, Theme.Muted, margin: Ui.Pad(2, 0, 0, 12));
            AddRow(infoLabel);

            calendar = new WeekCalendar();
            calendar.SetAllowSelect(isTutor);
            calendar.SetAllowMove(isTutor);
            calendar.EventClicked += async item => await OnEventAsync(item);
            calendar.RangeSelected += async (start, end) => await OnRangeAsync(start, end);
            calendar.EventMoved += async (item, newStart) => await OnMovedAsync(item, newStart);
            calendar.EventContext += ShowEventMenu;
            AddRow(calendar);

            // havi nézet: egy napra kattintva az adott hét heti nézete nyílik meg
            monthView = new MonthCalendar();
            monthView.DayClicked += day =>
            {
                calendar.SetWeek(day);
                SetViewMode(false);
            };
            AddRow(monthView, animate: false);
            monthView.Visible = false;

            // ---------- jelmagyarázat ----------
            var legend = Ui.WrapRow();
            legend.Controls.Add(Ui.Chip("Szabad időpont", Theme.Success));
            legend.Controls.Add(Ui.Chip("Függőben", Theme.Warning));
            legend.Controls.Add(Ui.Chip("Visszaigazolva", Theme.MainBlue));
            legend.Controls.Add(Ui.Chip("Lezajlott", Theme.Muted));
            AddRow(legend);

            UpdateWeekLabel();
            await RefreshDataAsync();
        }

        // ------------------------------------------------------------
        // Hét léptetése
        // ------------------------------------------------------------

        /// <summary>A függő és a visszaigazolt közelgő foglalások mentése .ics fájlba (Google Naptár, Outlook, telefon).</summary>
        private async Task ExportAsync()
        {
            try
            {
                var pendingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=pending");
                var upcomingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
                await Task.WhenAll(pendingTask, upcomingTask);
                if (IsDisposed) return;

                var items = pendingTask.Result.Concat(upcomingTask.Result).OrderBy(b => b.StartTime).ToList();
                if (items.Count == 0)
                {
                    MessageBox.Show(FindForm(), "Most nincs exportálható (függő vagy közelgő) foglalásod.", "Exportálás",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dialog = new SaveFileDialog
                {
                    Title = "Foglalások exportálása",
                    Filter = "Naptár fájl (*.ics)|*.ics",
                    FileName = "tutorconnect-foglalasok.ics"
                };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                File.WriteAllText(dialog.FileName, IcsExport.Build(items, isTutor), new System.Text.UTF8Encoding(false));
                MessageBox.Show(FindForm(), $"{items.Count} foglalás elmentve. A fájlt behúzhatod a Google Naptárba vagy megnyithatod az Outlookban.",
                    "Exportálás kész", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült az exportálás", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(FindForm(), "A fájl mentése nem sikerült: " + ex.Message, "Nem sikerült az exportálás",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Heti nézetben egy hetet, havi nézetben egy hónapot lép (irány: -1 vagy 1).</summary>
        private void Shift(int direction)
        {
            if (monthMode) monthView.SetMonth(monthView.Month.AddMonths(direction));
            else calendar.SetWeek(calendar.WeekStart.AddDays(7 * direction));
            UpdateWeekLabel();
        }

        private void GoToday()
        {
            calendar.SetWeek(DateTime.Today);
            monthView.SetMonth(DateTime.Today);
            UpdateWeekLabel();
        }

        private void SetViewMode(bool month)
        {
            monthMode = month;
            if (month) monthView.SetMonth(calendar.WeekStart.AddDays(3)); // a hét közepe adja a hónapot

            calendar.Visible = !month;
            monthView.Visible = month;
            weekChip.SetSelected(!month);
            monthChip.SetSelected(month);
            UpdateWeekLabel();
        }

        private void UpdateWeekLabel()
        {
            if (monthMode)
            {
                weekLabel.Text = monthView.Month.ToString("yyyy. MMMM", Ui.Hu);
                return;
            }

            var start = calendar.WeekStart;
            var end = start.AddDays(6);
            weekLabel.Text = start.Month == end.Month
                ? $"{start.ToString("yyyy. MMMM d.", Ui.Hu)} – {end.Day}."
                : $"{start.ToString("yyyy. MMMM d.", Ui.Hu)} – {end.ToString("MMMM d.", Ui.Hu)}";
        }

        // ------------------------------------------------------------
        // Adatok
        // ------------------------------------------------------------

        private async Task RefreshDataAsync()
        {
            int myRefresh = ++refreshId;
            try
            {
                var pendingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=pending");
                var upcomingTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=upcoming");
                var pastTask = ApiClient.GetAsync<List<BookingDto>>("/api/bookings?status=past");

                Task<List<SlotItemDto>>? slotsTask = isTutor ? ApiClient.GetAsync<List<SlotItemDto>>("/api/availability") : null;

                Task<AvailabilityDto>? tutorTask = null;
                TutorDto? selected = SelectedTutor();
                if (!isTutor && selected != null)
                    tutorTask = ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{selected.Id}/availability");

                var all = new List<Task> { pendingTask, upcomingTask, pastTask };
                if (slotsTask != null) all.Add(slotsTask);
                if (tutorTask != null) all.Add(tutorTask);
                await Task.WhenAll(all);

                if (IsDisposed || myRefresh != refreshId) return;

                bookings = pendingTask.Result.Concat(upcomingTask.Result).Concat(pastTask.Result)
                    .GroupBy(b => b.Id).Select(g => g.First()).ToList();
                mySlots = slotsTask?.Result ?? new List<SlotItemDto>();
                tutorAvailability = tutorTask?.Result;

                infoLabel.ForeColor = Theme.Muted;
                infoLabel.Text = DescribeState(selected);
                var events = BuildEvents();
                calendar.SetEvents(events);
                monthView.SetEvents(events);
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                infoLabel.ForeColor = Theme.Danger;
                infoLabel.Text = ex.Message;
            }
        }

        private string DescribeState(TutorDto? selected)
        {
            if (isTutor)
                return mySlots.Count(s => !s.IsBooked) == 0
                    ? "Még nincs szabad időpontod. Húzd végig az egeret egy üres részen az első megadásához."
                    : "Kattints egy szabad időpontra a törléséhez, húzd át egy másik időpontra, vagy jobb gombbal másold. Foglalásra kattintva a részletek nyílnak meg.";

            if (selected == null)
                return "Válassz oktatót a fenti listából, hogy lásd a szabad időpontjait. A saját óráid mindig látszanak.";

            return tutorAvailability is { Slots.Count: > 0 }
                ? $"{selected.FullName} szabad időpontjai zöldek: kattints egyre a jelentkezéshez."
                : $"{selected.FullName} jelenleg nem adott meg szabad időpontot.";
        }

        private TutorDto? SelectedTutor()
        {
            if (tutorBox == null || tutorBox.SelectedIndex <= 0) return null;
            int index = tutorBox.SelectedIndex - 1;
            return index < tutors.Count ? tutors[index] : null;
        }

        private List<CalEvent> BuildEvents()
        {
            var result = new List<CalEvent>();

            foreach (var booking in bookings)
            {
                CalKind? kind = booking.Status switch
                {
                    "PENDING" => CalKind.Pending,
                    "CONFIRMED" => CalKind.Confirmed,
                    "COMPLETED" => CalKind.Completed,
                    _ => null
                };
                if (kind == null) continue;

                result.Add(new CalEvent
                {
                    Start = booking.StartTime.ToLocalTime(),
                    End = booking.EndTime.ToLocalTime(),
                    Kind = kind.Value,
                    Title = booking.SubjectName ?? "Óra",
                    Subtitle = booking.OtherName ?? "",
                    Tag = booking
                });
            }

            if (isTutor)
            {
                foreach (var slot in mySlots)
                {
                    var start = slot.StartTime.ToLocalTime();
                    var end = slot.EndTime.ToLocalTime();

                    if (slot.IsBooked)
                    {
                        // a lefoglalt időponthoz tartozó foglalás már látszik; ha nincs, általános jelölést kap
                        bool shown = result.Any(r => r.Tag is BookingDto && Math.Abs((r.Start - start).TotalMinutes) < 1);
                        if (!shown)
                            result.Add(new CalEvent { Start = start, End = end, Kind = CalKind.Booked, Title = "Lefoglalva", Tag = slot });
                    }
                    else
                    {
                        result.Add(new CalEvent { Start = start, End = end, Kind = CalKind.Free, Title = "Szabad", Subtitle = "Kattints a törléshez", Tag = slot });
                    }
                }
            }
            else if (tutorAvailability != null)
            {
                foreach (var slot in tutorAvailability.Slots)
                {
                    result.Add(new CalEvent
                    {
                        Start = slot.StartTime.ToLocalTime(),
                        End = slot.EndTime.ToLocalTime(),
                        Kind = CalKind.Free,
                        Title = "Szabad",
                        Subtitle = "Jelentkezés",
                        Tag = slot
                    });
                }
            }

            return result;
        }

        // ------------------------------------------------------------
        // Műveletek
        // ------------------------------------------------------------

        private async Task OnRangeAsync(DateTime start, DateTime end)
        {
            if (!isTutor) return;

            // szabadságra jelölt napra csak megerősítéssel adható meg időpont
            if (TimeOffStore.IsBlocked(start) &&
                MessageBox.Show(FindForm(), "Erre a napra szabadságot jelöltél. Mégis megadsz rá időpontot?", "Szabadság",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            using var dialog = new AvailabilityDialog(start, (int)(end - start).TotalMinutes);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), dialog.ResultMessage, "Időpont mentve", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshDataAsync();
        }

        private async Task OnEventAsync(CalEvent item)
        {
            if (item.Kind == CalKind.Free)
            {
                if (isTutor && item.Tag is SlotItemDto mine)
                    await DeleteSlotAsync(mine);
                else if (!isTutor && item.Tag is SlotDto slot)
                    await ApplyAsync(slot);
                return;
            }

            if (item.Tag is BookingDto booking)
                await BookingDetailsAsync(booking);
            else
                MessageBox.Show(FindForm(), "Ez az időpont már le van foglalva.", "Naptár", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task OpenTimeOffAsync()
        {
            using var dialog = new TimeOffDialog();
            bool saved = dialog.ShowDialog(FindForm()) == DialogResult.OK;
            if (IsDisposed) return;

            // a szabadságok listája a megerősítés nélkül is módosulhatott (törlés)
            calendar.Invalidate();
            monthView.Invalidate();

            if (!saved) return;
            MessageBox.Show(FindForm(), dialog.ResultMessage, "Szabadság", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshDataAsync();
        }

        private static string StartText(DateTime local) =>
            local.ToString("yyyy-MM-dd'T'HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Szabad időpont áthelyezése: a régi törlődik, az új létrejön (ha az új nem sikerül, a régi visszaáll).</summary>
        private async Task OnMovedAsync(CalEvent item, DateTime newStart)
        {
            if (!isTutor || item.Tag is not SlotItemDto slot) return;

            if (newStart <= DateTime.Now)
            {
                MessageBox.Show(FindForm(), "Múltbeli időpontra nem lehet áthelyezni.", "Áthelyezés", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int minutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
            var newEnd = newStart.AddMinutes(minutes);
            string text = $"Áthelyezed a szabad időpontot?\n\nEddig: {Ui.FormatRange(slot.StartTime, slot.EndTime)}\nEzután: {Ui.FormatRange(newStart, newEnd)}";
            if (MessageBox.Show(FindForm(), text, "Áthelyezés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var oldStart = slot.StartTime.ToLocalTime();
            try
            {
                await ApiClient.DeleteAsync($"/api/availability/{slot.Id}");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Az áthelyezés nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (!IsDisposed) await RefreshDataAsync();
                return;
            }

            try
            {
                var result = await ApiClient.PostAsync<AvailabilityResultDto>("/api/availability", new Dictionary<string, object?>
                {
                    ["start"] = StartText(newStart),
                    ["minutes"] = minutes,
                    ["repeat_weeks"] = 1
                });
                if (result.Created == 0)
                    throw new ApiException("Az új időpont ütközik egy meglévővel, vagy nem adható meg.");
            }
            catch (ApiException ex)
            {
                // az eredeti időpont visszaállítása
                try
                {
                    await ApiClient.PostAsync<AvailabilityResultDto>("/api/availability", new Dictionary<string, object?>
                    {
                        ["start"] = StartText(oldStart),
                        ["minutes"] = minutes,
                        ["repeat_weeks"] = 1
                    });
                }
                catch (ApiException)
                {
                    // ha ez sem megy, a frissítés után látszik az állapot
                }

                MessageBox.Show(FindForm(), ex.Message + "\nAz eredeti időpont megmaradt.", "Az áthelyezés nem sikerült",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (!IsDisposed) await RefreshDataAsync();
        }

        /// <summary>Szabad időpont másolása a megadott számú héttel későbbre.</summary>
        private async Task CopySlotAsync(SlotItemDto slot, int weeks)
        {
            try
            {
                int minutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
                var result = await ApiClient.PostAsync<AvailabilityResultDto>("/api/availability", new Dictionary<string, object?>
                {
                    ["start"] = StartText(slot.StartTime.ToLocalTime().AddDays(7 * weeks)),
                    ["minutes"] = minutes,
                    ["repeat_weeks"] = 1
                });

                MessageBox.Show(FindForm(),
                    result.Created > 0 ? "Az időpont lemásolva a következő hétre." : "Ott már van időpont, vagy nem adható meg.",
                    "Másolás", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A másolás nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!IsDisposed) await RefreshDataAsync();
        }

        /// <summary>A látható hét szabad (még le nem foglalt) időpontjait lemásolja a következő hétre.</summary>
        private async Task CopyWeekAsync()
        {
            var start = calendar.WeekStart;
            var end = start.AddDays(7);
            var slots = mySlots
                .Where(s => !s.IsBooked && s.StartTime.ToLocalTime() >= start && s.StartTime.ToLocalTime() < end)
                .ToList();

            if (slots.Count == 0)
            {
                MessageBox.Show(FindForm(), "Ezen a héten nincs másolható szabad időpontod.", "Hét másolása", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(FindForm(), $"Lemásolod a hét {slots.Count} szabad időpontját a következő hétre?", "Hét másolása",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            int created = 0;
            int skipped = 0;
            try
            {
                foreach (var slot in slots)
                {
                    int minutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
                    var result = await ApiClient.PostAsync<AvailabilityResultDto>("/api/availability", new Dictionary<string, object?>
                    {
                        ["start"] = StartText(slot.StartTime.ToLocalTime().AddDays(7)),
                        ["minutes"] = minutes,
                        ["repeat_weeks"] = 1
                    });
                    created += result.Created;
                    skipped += result.Skipped;
                    if (IsDisposed) return;
                }
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message + $"\nEddig {created} időpont jött létre.", "A másolás megszakadt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (!IsDisposed) await RefreshDataAsync();
                return;
            }

            MessageBox.Show(FindForm(),
                $"{created} időpont lemásolva." + (skipped > 0 ? $" {skipped} kimaradt (ütközés vagy túl messze van)." : ""),
                "Hét másolása", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (!IsDisposed)
            {
                calendar.SetWeek(start.AddDays(7)); // a másolat hetére lépünk
                UpdateWeekLabel();
                await RefreshDataAsync();
            }
        }

        /// <summary>Jobb gombos menü egy bejegyzésen: törlés, másolás, részletek.</summary>
        private void ShowEventMenu(CalEvent item, Point screenPoint)
        {
            var menu = new ContextMenuStrip();
            menu.Closed += (_, _) => BeginInvoke(new Action(menu.Dispose));

            if (item.Kind == CalKind.Free && isTutor && item.Tag is SlotItemDto slot)
            {
                menu.Items.Add("Másolás a következő hétre", null, async (_, _) => await CopySlotAsync(slot, 1));
                menu.Items.Add("Másolás két hét múlvára", null, async (_, _) => await CopySlotAsync(slot, 2));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Törlés", null, async (_, _) => await DeleteSlotAsync(slot));
            }
            else if (item.Kind == CalKind.Free && !isTutor)
            {
                menu.Items.Add("Jelentkezés", null, async (_, _) => await OnEventAsync(item));
            }
            else
            {
                menu.Items.Add("Részletek", null, async (_, _) => await OnEventAsync(item));
            }

            menu.Show(screenPoint);
        }

        private async Task DeleteSlotAsync(SlotItemDto slot)
        {
            string when = Ui.FormatRange(slot.StartTime, slot.EndTime);
            if (MessageBox.Show(FindForm(), $"Törlöd ezt a szabad időpontot?\n\n{when}", "Időpont törlése",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.DeleteAsync($"/api/availability/{slot.Id}");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A törlés nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!IsDisposed) await RefreshDataAsync();
        }

        private async Task ApplyAsync(SlotDto slot)
        {
            var selected = SelectedTutor();
            if (selected == null || tutorAvailability == null) return;

            using var dialog = new QuickBookDialog(selected.FullName, slot, tutorAvailability.Subjects);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), "A jelentkezést elküldtük. Az oktató visszaigazolására vár.", "Jelentkezés elküldve",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            AppEvents.RaiseBadgesStale();
            await RefreshDataAsync();
        }

        private async Task BookingDetailsAsync(BookingDto booking)
        {
            string details =
                $"{booking.SubjectName ?? "Óra"}\n" +
                $"{(booking.AsStudent ? "Oktató" : "Diák")}: {booking.OtherName ?? "ismeretlen"}\n" +
                $"{Ui.FormatRange(booking.StartTime, booking.EndTime)}\n" +
                (booking.Price is > 0 ? $"Ár: {Ui.Money(booking.Price.Value)}\n" : "") +
                (string.IsNullOrWhiteSpace(booking.Note) ? "" : $"Üzenet: {booking.Note}\n");

            string? action = null;

            if (booking.CanConfirm && booking.CanReject)
            {
                var answer = MessageBox.Show(FindForm(), details + "\nElfogadod a foglalási kérelmet?\n\nIgen = elfogadás, Nem = elutasítás",
                    "Foglalási kérelem", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                action = answer switch
                {
                    DialogResult.Yes => "confirm",
                    DialogResult.No => "reject",
                    _ => null
                };
            }
            else if (booking.CanCancel)
            {
                var answer = MessageBox.Show(FindForm(), details + "\nSzeretnéd lemondani ezt az órát?", "Foglalás",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes) action = "cancel";
            }
            else
            {
                MessageBox.Show(FindForm(), details, "Foglalás", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (action == null) return;

            try
            {
                await ApiClient.PostAsync($"/api/bookings/{booking.Id}/{action}");
                AppEvents.RaiseBadgesStale();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A művelet nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!IsDisposed) await RefreshDataAsync();
        }

        private async void OnRealtime(RealtimeEvent e)
        {
            // egy másik felhasználó foglalt vagy módosított: a naptár csendben frissül
            if (IsDisposed || e.Type != "booking_update" || calendar == null) return;
            await RefreshDataAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Realtime.EventReceived -= OnRealtime;
            base.Dispose(disposing);
        }
    }

    // ================================================================
    // Jelentkezés egy kiválasztott szabad időpontra
    // ================================================================

    /// <summary>Gyors jelentkezés a naptárból: tantárgy kiválasztása és opcionális üzenet.</summary>
    internal sealed class QuickBookDialog : DialogBase
    {
        private readonly SlotDto slot;
        private readonly List<(SelectableChip Chip, SlotSubjectDto Subject)> chips = new();
        private readonly TextBox note;
        private SlotSubjectDto? selected;

        public QuickBookDialog(string tutorName, SlotDto slot, List<SlotSubjectDto> subjects)
            : base("Jelentkezés órára", $"{tutorName}  ·  {Ui.FormatRange(slot.StartTime, slot.EndTime)}", "Jelentkezés", Ui.Sz(560, 520), "\uE787")
        {
            this.slot = slot;

            Section("1   Válassz tantárgyat");
            var flow = NewWrap();
            if (subjects.Count == 0)
                flow.Controls.Add(Ui.Lbl("Az oktató még nem adott meg tantárgyat.", 10F, Theme.Muted));

            foreach (var subject in subjects)
            {
                string text = subject.HourlyRate > 0 ? $"{subject.Name} · {Ui.Money(subject.HourlyRate)}/óra" : subject.Name;
                var chip = new SelectableChip(text);
                chip.Click += (_, _) => ChooseSubject(subject);
                flow.Controls.Add(chip);
                chips.Add((chip, subject));
            }
            AddBody(flow);

            Section("2   Üzenet az oktatónak (nem kötelező)");
            note = NewTextBox(90, 300);
            AddBody(note);

            ActionButton.Enabled = false;
            SetFooter("Válassz tantárgyat.");
            if (subjects.Count == 1) ChooseSubject(subjects[0]);
        }

        private void ChooseSubject(SlotSubjectDto subject)
        {
            selected = subject;
            foreach (var (chip, item) in chips)
                chip.SetSelected(item == subject);

            string price = subject.HourlyRate > 0 ? $"\nÓradíj: {Ui.Money(subject.HourlyRate)}" : "";
            SetFooter($"{subject.Name}\n{Ui.FormatRange(slot.StartTime, slot.EndTime)}{price}");
            ActionButton.Enabled = true;
        }

        protected override async Task OnActionAsync()
        {
            if (selected == null) return;

            ActionButton.Enabled = false;
            try
            {
                await ApiClient.PostAsync("/api/bookings", new Dictionary<string, object?>
                {
                    ["availability_id"] = slot.Id,
                    ["subject_id"] = selected.Id,
                    ["note"] = note.Text.Trim()
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
