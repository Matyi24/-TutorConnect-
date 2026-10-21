using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>
    /// A dialógusok közös váza: színátmenetes fejléc (ikon, cím, alcím), görgethető tartalom,
    /// lábléc (állapotsor + Mégse + művelet-gomb). Megjelenéskor halványan beúszik, a keret fekete,
    /// a színek a témát követik. A megadott méretnél kicsit nagyobb ablak nyílik (a képernyőre korlátozva).
    /// </summary>
    internal abstract class DialogBase : Form
    {
        /// <summary>A tartalom (pirulák, beviteli mezők) szélessége; a képernyő nagyításával együtt nő.</summary>
        protected static int ContentWidth => Ui.S(528);

        private readonly FlowLayoutPanel root;
        private SectionGroup? group;

        /// <summary>A tartalom helye: az aktuális szakasz doboza (a Section hívás után), korábban maga a görgetett felület.</summary>
        protected FlowLayoutPanel Body => group ?? root;

        protected readonly HoverButton ActionButton;
        protected readonly HoverButton DismissButton;
        private readonly Label footerLabel;

        protected DialogBase(string title, string subtitle, string actionText, Size clientSize, string glyph = "")
        {
            // nagyobb ablak a korábbinál, de nem lóg ki a képernyőről
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            clientSize = new Size(
                Math.Min(clientSize.Width + Ui.S(80), area.Width - Ui.S(60)),
                Math.Min(clientSize.Height + Ui.S(70), area.Height - Ui.S(110)));

            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = clientSize;
            BackColor = Theme.CardBg;
            ForeColor = Theme.Text;
            Font = Ui.MakeFont(10F);
            Opacity = 0; // az OnShown-ban úszik be

            var header = new DialogHeader(title, subtitle, glyph, clientSize.Width) { Dock = DockStyle.Top };

            // sima görgetésű tartalom; a szakaszok dobozai (SectionGroup) ennek a panelnek a gyerekei
            var scroll = new SmoothScrollPanel { Dock = DockStyle.Fill, BackColor = Theme.CardBg };
            root = scroll.Content;
            root.Padding = Ui.Pad(28, 18, 12, 14);

            int footerHeight = Ui.S(104);
            var footer = new Panel { Dock = DockStyle.Bottom, Height = footerHeight, BackColor = Theme.PageBg };
            footer.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.CardBorder);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            int buttonHeight = Ui.S(48);
            int buttonY = (footerHeight - buttonHeight) / 2;
            int actionWidth = Ui.S(214);
            int dismissWidth = Ui.S(120);
            int right = clientSize.Width - Ui.S(28);

            ActionButton = Ui.PrimaryBtn(actionText, async (_, _) => await OnActionAsync());
            ActionButton.AutoSize = false;
            ActionButton.Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            ActionButton.Bounds = new Rectangle(right - actionWidth, buttonY, actionWidth, buttonHeight);
            ActionButton.Margin = Ui.Pad(0);

            DismissButton = Ui.Btn("Mégse", Theme.CardBg, Theme.Text, null, outline: true);
            DismissButton.AutoSize = false;
            DismissButton.Font = Ui.MakeFont(10.5F, FontStyle.Bold);
            DismissButton.Bounds = new Rectangle(ActionButton.Left - Ui.S(12) - dismissWidth, buttonY, dismissWidth, buttonHeight);
            DismissButton.Margin = Ui.Pad(0);
            DismissButton.DialogResult = DialogResult.Cancel; // Esc és a gomb is bezárja
            CancelButton = DismissButton;

            footerLabel = new Label
            {
                Bounds = new Rectangle(Ui.S(28), Ui.S(12), Math.Max(DismissButton.Left - Ui.S(16) - Ui.S(28), Ui.S(80)), footerHeight - Ui.S(24)),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            footer.Controls.Add(footerLabel);
            footer.Controls.Add(DismissButton);
            footer.Controls.Add(ActionButton);

            Controls.Add(scroll); // a kitöltő elem legyen elöl a sorrendben
            Controls.Add(header);
            Controls.Add(footer);
        }

        /// <summary>A művelet-gombra kattintáskor fut le.</summary>
        protected abstract Task OnActionAsync();

        /// <summary>
        /// Új szakasz: külön doboz címmel és elválasztó vonallal; a következő AddBody / Body.Controls.Add hívások
        /// ebbe kerülnek a következő Section-ig. Az "1   Szöveg" alakú címeknél a szám kék körben jelenik meg.
        /// </summary>
        protected void Section(string text)
        {
            group = new SectionGroup(ContentWidth + Ui.S(40));
            root.Controls.Add(group);
            group.Controls.Add(new SectionHeading(text, ContentWidth));
        }

        protected void AddBody(Control control)
        {
            control.Margin = Ui.Pad(0, 0, 0, 10);
            Body.Controls.Add(control);
        }

        protected void SetFooter(string text, bool error = false)
        {
            footerLabel.ForeColor = error ? Theme.Danger : Theme.Muted;
            footerLabel.Text = text;
        }

        /// <summary>Tördelődő, automatikus magasságú sor a tartalom szélességére korlátozva.</summary>
        protected static FlowLayoutPanel NewWrap() => new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(ContentWidth, 0),
            BackColor = Color.Transparent,
            Margin = Ui.Pad(0, 0, 0, 6)
        };

        protected static TextBox NewInput(string text, int maxLength, bool password = false) => new()
        {
            Width = ContentWidth,
            MaxLength = maxLength,
            Text = text,
            UseSystemPasswordChar = password,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F)
        };

        protected static TextBox NewTextBox(int height, int maxLength) => new()
        {
            Width = ContentWidth,
            Height = Ui.S(height),
            Multiline = true,
            MaxLength = maxLength,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F)
        };

        protected static ComboBox NewCombo(int width) => new()
        {
            Width = Ui.S(width),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.InputBg,
            ForeColor = Theme.Text,
            Font = Ui.MakeFont(11F),
            Margin = Ui.Pad(0, 0, 16, 0)
        };

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Update(); // előbb a teljes tartalom kifestődik, csak utána kezd beúszni (nem látszik félkész állapot)
            Anim.Run(220, t => Opacity = t);
        }
    }
}
