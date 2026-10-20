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
                var previous = hover;
                hover = item;
                Cursor = item != null ? Cursors.Hand : (allowSelect && InGrid(e.Location) ? Cursors.Cross : Cursors.Default);

                // csak a két érintett bejegyzést fessük újra, nem az egész naptárat
                InvalidateEvent(previous);
                InvalidateEvent(item);
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
                var previous = hover;
                hover = null;
                InvalidateEvent(previous);
            }
        }

        /// <summary>Egy bejegyzés területének újrafestése (a vastagabb hover-keret miatt kis ráhagyással).</summary>
        private void InvalidateEvent(CalEvent? item)
        {
            if (item == null) return;

            foreach (var (candidate, rect) in layout)
            {
                if (candidate != item) continue;
                Invalidate(Rectangle.Inflate(rect, Ui.S(4), Ui.S(4)));
                return;
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
}
