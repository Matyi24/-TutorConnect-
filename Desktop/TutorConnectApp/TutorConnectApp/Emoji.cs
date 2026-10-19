using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Emoji-választó rács a chat szövegmezőjéhez. Kattintásra az emoji bekerül az üzenetbe.</summary>
    internal sealed class EmojiPicker : Control
    {
        private static readonly string[] Emojis =
        {
            "😀", "😁", "😂", "🤣", "😊", "😇", "🙂", "😉",
            "😍", "🥰", "😘", "😎", "🤓", "🤔", "😅", "😆",
            "😢", "😭", "😡", "😴", "🤯", "😳", "🙄", "😬",
            "👍", "👎", "👏", "🙌", "🙏", "💪", "👋", "🤝",
            "✅", "❌", "⭐", "🔥", "💡", "🎉", "🎓", "📚",
            "📝", "✏️", "📅", "⏰", "💬", "📎", "📞", "💻",
            "❤️", "💙", "💯", "👀", "🍀", "☕", "🌟", "😋"
        };

        private const int Columns = 8;

        // a színes emoji-betűkészlet (Windows 10+); egyszer létrehozva, nem rajzoláskor
        private static readonly Font EmojiFont = new("Segoe UI Emoji", 15F, FontStyle.Regular, GraphicsUnit.Point);

        private int hover = -1;

        public event Action<string>? EmojiChosen;

        public EmojiPicker()
        {
            int cell = Cell;
            int rows = (Emojis.Length + Columns - 1) / Columns;
            Size = new Size(cell * Columns + Ui.S(16), cell * rows + Ui.S(16));
            BackColor = Theme.CardBg;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private static int Cell => Ui.S(44);

        private Rectangle CellRect(int index) =>
            new(Ui.S(8) + index % Columns * Cell, Ui.S(8) + index / Columns * Cell, Cell, Cell);

        private int IndexAt(Point point)
        {
            int column = (point.X - Ui.S(8)) / Cell;
            int row = (point.Y - Ui.S(8)) / Cell;
            if (point.X < Ui.S(8) || point.Y < Ui.S(8) || column < 0 || column >= Columns || row < 0) return -1;

            int index = row * Columns + column;
            return index < Emojis.Length ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = IndexAt(e.Location);
            if (index == hover) return;
            hover = index;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int index = IndexAt(e.Location);
            if (index >= 0) EmojiChosen?.Invoke(Emojis[index]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.CardBg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var border = new Pen(Theme.CardBorder);
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var brush = new SolidBrush(Theme.Text);
            for (int i = 0; i < Emojis.Length; i++)
            {
                var rect = CellRect(i);
                if (i == hover)
                {
                    using var path = Ui.RoundedPath(Rectangle.Inflate(rect, -Ui.S(2), -Ui.S(2)), Ui.S(8));
                    using var fill = new SolidBrush(Theme.SoftHover);
                    g.FillPath(fill, path);
                }
                g.DrawString(Emojis[i], EmojiFont, brush, rect, center);
            }
        }

        /// <summary>Megnyitja a választót a gomb fölött; a kiválasztott emojit az onChosen kapja meg.</summary>
        public static void ShowAbove(Control anchor, Action<string> onChosen)
        {
            var picker = new EmojiPicker();
            var host = new ToolStripControlHost(picker)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = picker.Size
            };

            var popup = new ToolStripDropDown
            {
                Padding = Padding.Empty,
                AutoClose = true,
                DropShadowEnabled = true,
                BackColor = Theme.CardBg
            };
            popup.Items.Add(host);

            picker.EmojiChosen += text =>
            {
                onChosen(text);
                popup.Close();
            };
            // a felület bezárása után szabadítjuk fel (a Closed eseményben még használatban van)
            popup.Closed += (_, _) => anchor.BeginInvoke(new Action(popup.Dispose));

            popup.Show(anchor, new Point(0, -picker.Height - Ui.S(6)));
        }
    }
}
