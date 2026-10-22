using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>
    /// A foglalás "jegye": balra a dátum nagy csempén (hónap, nap, a hét napja) az állapot színében, mellette a tantárgy,
    /// a partner, az időpont és az ár; jobb felül az állapot és a hátralévő idő. Így elsőre látszik, mikor, kivel és mi a helyzet.
    /// </summary>
    internal sealed class BookingTicket : Control
    {
        private readonly BookingDto booking;
        private readonly Color accent;
        private readonly string statusText;
        private readonly string relative;

        public BookingTicket(BookingDto booking, Color accent, string statusText)
        {
            this.booking = booking;
            this.accent = accent;
            this.statusText = statusText;
            relative = booking.Status is "PENDING" or "CONFIRMED" ? BookingText.Relative(booking.StartTime, DateTime.UtcNow) : "";

            Tag = "fill";
            Height = Ui.S(132); // három adatsor és a cím is kényelmesen elfér (az ár sem vágódik le)
            Margin = Ui.Pad(0, 0, 0, 10);
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = $"{booking.SubjectName ?? "Tantárgy"}, {(booking.AsStudent ? "oktató" : "diák")}: {booking.OtherName}, "
                + $"{Ui.FormatRange(booking.StartTime, booking.EndTime)}, {BookingText.Duration(booking.EndTime - booking.StartTime)}"
                + (booking.Price is > 0 ? ", " + Ui.Money(booking.Price.Value) : "") + $", {statusText}"
                + (relative.Length > 0 ? ", " + relative : "");
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (Width < Ui.S(260)) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var start = booking.StartTime.ToLocalTime();
            bool faded = booking.Status is "CANCELLED" or "REJECTED";
            Color tileText = faded ? Theme.Muted : accent;

            // ---------- dátum-csempe ----------
            var tile = new Rectangle(0, Ui.S(4), Ui.S(84), Height - Ui.S(8));
            using (var path = Ui.RoundedPath(tile, Ui.S(16)))
            {
                using var fill = new SolidBrush(Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.22 : 0.11));
                using var border = new Pen(Anim.Lerp(Theme.CardBorder, accent, 0.55), Ui.S(1.4F));
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, BookingText.MonthShort(start), Ui.MakeFont(9.5F, FontStyle.Bold),
                new Rectangle(tile.X, tile.Y + Ui.S(10), tile.Width, Ui.S(18)), tileText, center);
            TextRenderer.DrawText(g, start.Day.ToString(), Ui.MakeFont(26F, FontStyle.Bold),
                new Rectangle(tile.X, tile.Y + Ui.S(26), tile.Width, Ui.S(40)), faded ? Theme.Muted : Theme.Text, center);
            TextRenderer.DrawText(g, start.ToString("dddd", Ui.Hu), Ui.MakeFont(9F),
                new Rectangle(tile.X, tile.Bottom - Ui.S(26), tile.Width, Ui.S(18)), Theme.Muted, center | TextFormatFlags.EndEllipsis);

            // ---------- jobb felül: állapot és hátralévő idő ----------
            var pillFont = Ui.MakeFont(9F, FontStyle.Bold);
            int pillWidth = TextRenderer.MeasureText(statusText, pillFont).Width + Ui.S(36);
            var pill = new Rectangle(Width - pillWidth, Ui.S(6), pillWidth, Ui.S(28));
            using (var pillPath = Ui.RoundedPath(pill, pill.Height / 2))
            {
                using var fill = new SolidBrush(Anim.Lerp(Theme.CardBg, accent, Theme.Dark ? 0.26 : 0.14));
                g.FillPath(fill, pillPath);
            }
            using (var dot = new SolidBrush(accent))
                g.FillEllipse(dot, pill.X + Ui.S(12), pill.Y + (pill.Height - Ui.S(8)) / 2, Ui.S(8), Ui.S(8));
            TextRenderer.DrawText(g, statusText, pillFont, new Rectangle(pill.X + Ui.S(26), pill.Y, pill.Width - Ui.S(30), pill.Height), accent,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            int textX = tile.Right + Ui.S(20);
            int rightLimit = pill.X - Ui.S(12);

            if (relative.Length > 0)
            {
                TextRenderer.DrawText(g, relative, Ui.MakeFont(9.5F, FontStyle.Bold),
                    new Rectangle(pill.X - Ui.S(2), pill.Bottom + Ui.S(4), pill.Width + Ui.S(2), Ui.S(20)), Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }

            // ---------- szövegek ----------
            TextRenderer.DrawText(g, booking.SubjectName ?? "Tantárgy", Ui.MakeFont(14F, FontStyle.Bold),
                new Rectangle(textX, Ui.S(6), Math.Max(rightLimit - textX, 0), Ui.S(28)), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            int y = Ui.S(42);
            string role = booking.AsStudent ? "Oktató" : "Diák";
            DrawLine(g, "", textX, y, Width - textX, role + ": ", booking.OtherName ?? "ismeretlen");
            y += Ui.S(28);

            string span = $"{start:HH:mm} – {booking.EndTime.ToLocalTime():HH:mm}";
            string length = BookingText.Duration(booking.EndTime - booking.StartTime);
            DrawLine(g, "", textX, y, Width - textX, "", length.Length > 0 ? $"{span}  ·  {length}" : span);
            y += Ui.S(28);

            if (booking.Price is > 0)
                DrawLine(g, "", textX, y, Width - textX, "", Ui.Money(booking.Price.Value), boldValue: true);
        }

        private static void DrawLine(Graphics g, string glyph, int x, int y, int width, string label, string value, bool boldValue = false)
        {
            using (var iconBrush = new SolidBrush(Theme.MainBlue))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, Ui.IconFont(11F), iconBrush, new Rectangle(x, y, Ui.S(22), Ui.S(24)), center);

            int textX = x + Ui.S(30);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            if (label.Length > 0)
            {
                var labelFont = Ui.MakeFont(10.5F);
                int labelWidth = TextRenderer.MeasureText(label, labelFont, new Size(int.MaxValue, 0), TextFormatFlags.NoPadding).Width;
                TextRenderer.DrawText(g, label, labelFont, new Rectangle(textX, y, labelWidth + Ui.S(4), Ui.S(24)), Theme.Muted, flags);
                textX += labelWidth;
            }

            TextRenderer.DrawText(g, value, Ui.MakeFont(10.5F, boldValue || label.Length > 0 ? FontStyle.Bold : FontStyle.Regular),
                new Rectangle(textX, y, Math.Max(width - (textX - x) - Ui.S(8), 0), Ui.S(24)), Theme.Text, flags | TextFormatFlags.EndEllipsis);
        }
    }
}
