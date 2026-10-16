using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Egy sor az oktatóválasztó listában: avatar, név, tantárgyak és értékelés. Kattintásra kijelölődik.</summary>
    internal sealed class TutorPickItem : Control
    {
        public readonly TutorDto Tutor;
        private readonly bool hasConversation;
        private bool selected;
        private bool hover;

        public TutorPickItem(TutorDto tutor, bool hasConversation, int width)
        {
            Tutor = tutor;
            this.hasConversation = hasConversation;
            Size = new Size(width, Ui.S(66));
            Margin = Ui.Pad(0, 0, 0, 6);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var card = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Ui.RoundedPath(card, Ui.S(12)))
            {
                using var fill = new SolidBrush(selected ? Theme.ChipBg : hover ? Theme.SoftHover : Theme.CardBg);
                using var border = new Pen(selected ? Theme.MainBlue : Theme.CardBorder, selected ? 2F : 1F);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int avatarSize = Ui.S(44);
            var avatar = new Rectangle(Ui.S(12), (Height - avatarSize) / 2, avatarSize, avatarSize);
            Ui.DrawAvatar(g, avatar, Ui.Initials(Tutor.FullName), Theme.MainBlue, Theme.White);

            int textX = avatar.Right + Ui.S(12);
            string rating = Tutor.ReviewCount > 0 ? $"★ {Tutor.AverageRating.ToString("0.0", Ui.Hu)}" : "";
            int ratingWidth = rating.Length == 0 ? 0 : TextRenderer.MeasureText(rating, Ui.MakeFont(10F, FontStyle.Bold)).Width + Ui.S(8);
            int right = Width - Ui.S(14);

            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, Tutor.FullName, Ui.MakeFont(11F, FontStyle.Bold),
                new Rectangle(textX, Ui.S(10), Math.Max(right - ratingWidth - textX, 0), Ui.S(24)), Theme.Text, flags);

            if (rating.Length > 0)
                TextRenderer.DrawText(g, rating, Ui.MakeFont(10F, FontStyle.Bold),
                    new Rectangle(right - ratingWidth, Ui.S(12), ratingWidth, Ui.S(22)), Theme.Star, flags | TextFormatFlags.Right);

            string subtitle = hasConversation
                ? "Már van beszélgetésetek"
                : (string.IsNullOrWhiteSpace(Tutor.Subjects) ? "Nincs megadott tantárgy" : Tutor.Subjects!);
            TextRenderer.DrawText(g, subtitle, Ui.MakeFont(9.5F),
                new Rectangle(textX, Ui.S(36), Math.Max(right - textX, 0), Ui.S(22)), hasConversation ? Theme.MainBlue : Theme.Muted, flags);
        }
    }

    /// <summary>Új beszélgetés indítása a Chat oldalról: keresés és kiválasztás az oktatók közül.</summary>
    internal sealed class NewConversationDialog : DialogBase
    {
        private const int MaxShown = 40;

        private readonly List<TutorDto> tutors;
        private readonly HashSet<int> existing;
        private readonly TextBox searchBox;
        private readonly FlowLayoutPanel list;
        private readonly List<TutorPickItem> items = new();
        private TutorDto? selected;

        /// <summary>A létrehozott (vagy már meglévő) beszélgetés azonosítója.</summary>
        public int ConversationId { get; private set; }

        public NewConversationDialog(List<TutorDto> tutors, IEnumerable<int> existingTutorIds)
            : base("Új beszélgetés", "Válaszd ki az oktatót, akinek üzenni szeretnél.", "Beszélgetés indítása", Ui.Sz(560, 700), "")
        {
            this.tutors = tutors.OrderBy(t => t.FullName, StringComparer.CurrentCultureIgnoreCase).ToList();
            existing = existingTutorIds.ToHashSet();

            Section("Keresés");
            searchBox = NewInput("", 60);
            searchBox.PlaceholderText = "Név vagy tantárgy alapján...";
            searchBox.TextChanged += (_, _) => Render();
            AddBody(searchBox);

            Section("Oktatók");
            list = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            AddBody(list);

            ActionButton.Enabled = false;
            SetFooter("Válassz egy oktatót a listából.");
            Render();
        }

        private void Render()
        {
            string query = searchBox.Text.Trim();
            var matches = tutors
                .Where(t => query.Length == 0
                            || Contains(t.FullName, query)
                            || Contains(t.Subjects ?? "", query))
                .ToList();

            list.SuspendLayout();
            Ui.Reset(list);
            items.Clear();

            if (matches.Count == 0)
            {
                list.Controls.Add(Ui.Lbl("Nincs a keresésnek megfelelő oktató.", 10.5F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 4)));
            }
            else
            {
                foreach (var tutor in matches.Take(MaxShown))
                {
                    var item = new TutorPickItem(tutor, existing.Contains(tutor.Id), ContentWidth);
                    item.SetSelected(selected?.Id == tutor.Id);
                    item.Click += (_, _) => Choose(tutor);
                    list.Controls.Add(item);
                    items.Add(item);
                }

                if (matches.Count > MaxShown)
                    list.Controls.Add(Ui.Lbl($"Még {matches.Count - MaxShown} oktató van: szűkítsd a keresést.", 9.5F, Theme.Muted, margin: Ui.Pad(2, 4, 0, 0)));
            }
            list.ResumeLayout(true);
        }

        /// <summary>Kis- és nagybetűre, ékezetre érzéketlen keresés ("o" megtalálja az "ő"-t is).</summary>
        private static bool Contains(string text, string query) =>
            System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                text, query, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;

        private void Choose(TutorDto tutor)
        {
            selected = tutor;
            foreach (var item in items)
                item.SetSelected(item.Tutor.Id == tutor.Id);

            ActionButton.Enabled = true;
            SetFooter(existing.Contains(tutor.Id)
                ? $"{tutor.FullName}\nMár van beszélgetésetek: megnyitjuk."
                : $"{tutor.FullName}\nÚj beszélgetés indul.");
        }

        protected override async Task OnActionAsync()
        {
            if (selected == null) return;

            ActionButton.Enabled = false;
            try
            {
                var conversation = await ApiClient.PostAsync<ConversationDto>("/api/conversations",
                    new Dictionary<string, object?> { ["tutor_id"] = selected.Id });
                ConversationId = conversation.Id;
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

    /// <summary>A diák profilja az oktatónak (iskolai szint, évfolyam, tantárgyak). E-mail cím nem jelenik meg.</summary>
    internal sealed class StudentProfileDialog : DialogBase
    {
        public StudentProfileDialog(StudentProfileDto student)
            : base(string.IsNullOrWhiteSpace(student.Name) ? "Diák" : student.Name, "Diák profilja", "Bezárás", Ui.Sz(560, 420), "")
        {
            Section("Tanulmányok");
            AddBody(Ui.KeyValue("Iskolai szint", string.IsNullOrWhiteSpace(student.SchoolLevel) ? "Nincs megadva" : student.SchoolLevel!));
            AddBody(Ui.KeyValue("Osztály / évfolyam", string.IsNullOrWhiteSpace(student.Grade) ? "Nincs megadva" : student.Grade!));

            Section("Ebből kér segítséget");
            if (student.Subjects.Count == 0)
            {
                AddBody(Ui.Lbl("A diák még nem adott meg tantárgyat.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            }
            else
            {
                var chips = NewWrap();
                foreach (string subject in student.Subjects)
                    chips.Controls.Add(Ui.Chip(subject));
                AddBody(chips);
            }

            DismissButton.Visible = false; // csak tájékoztató ablak: egy "Bezárás" gomb elég
            SetFooter("A diák e-mail címe nem látható.");
        }

        protected override Task OnActionAsync()
        {
            Close();
            return Task.CompletedTask;
        }
    }

    /// <summary>Képnézegető a chat képeihez: ablakhoz igazított nagy nézet, mentéssel. Esc vagy Bezárás zárja.</summary>
    internal sealed class ImageViewerForm : Form
    {
        private readonly string path;
        private readonly string fileName;
        private readonly Image image;
        private readonly MemoryStream stream;

        /// <summary>Betölti a képet; ArgumentException, ha a fájl nem olvasható képként.</summary>
        public ImageViewerForm(string path, string fileName)
        {
            this.path = path;
            this.fileName = fileName;

            // a fájlt nem zárolja (a memóriából olvas), és a stream a kép élettartamáig él
            stream = new MemoryStream(File.ReadAllBytes(path));
            try
            {
                image = Image.FromStream(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Text = fileName;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size((int)(area.Width * 0.7), (int)(area.Height * 0.75));
            MinimumSize = Ui.Sz(480, 360);
            BackColor = Theme.LoginDark1;
            ForeColor = Color.White;
            KeyPreview = true;
            Font = Ui.MakeFont(10F);

            var bar = new Panel { Dock = DockStyle.Top, Height = Ui.S(64), BackColor = Theme.LoginDark2 };
            var name = Ui.Lbl(fileName, 11F, Color.White, bold: true);
            name.BackColor = Color.Transparent;
            name.AutoSize = false;
            name.AutoEllipsis = true;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.Dock = DockStyle.Fill;
            name.Padding = Ui.Pad(24, 0, 0, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = Ui.Pad(0, 10, 16, 0)
            };
            var save = Ui.Btn("Mentés másként", Theme.White, Theme.MainBlueDark, (_, _) => SaveCopy());
            var close = Ui.Btn("Bezárás", Theme.LoginDark2, Theme.White, (_, _) => Close(), outline: true);
            close.ForeColor = Theme.Text;
            buttons.Controls.Add(save);
            buttons.Controls.Add(close);

            bar.Controls.Add(name);
            bar.Controls.Add(buttons);

            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Theme.LoginDark1,
                Image = image
            };
            picture.DoubleClick += (_, _) => Close();

            Controls.Add(picture); // a kitöltő elem legyen elöl a sorrendben
            Controls.Add(bar);
        }

        private void SaveCopy()
        {
            using var dialog = new SaveFileDialog { FileName = fileName, Title = "Kép mentése" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                File.Copy(path, dialog.FileName, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(this, "A fájl mentése nem sikerült: " + ex.Message, "Kép mentése", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyBlack(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                image.Dispose();
                stream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
