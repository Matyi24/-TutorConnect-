using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace TutorConnectApp
{
    /// <summary>Egy beszélgetés a listában: avatar, név, előnézet, idő és olvasatlan jelvény.</summary>
    internal sealed class ConversationItem : Control
    {
        public readonly ConversationDto Conversation;
        private bool selected;
        private double hoverT;
        private Anim.Tween? hoverTween;
        private string draft = "";

        public ConversationItem(ConversationDto conversation)
        {
            Conversation = conversation;
            UpdateAccessibleName();
            AccessibleRole = AccessibleRole.ListItem;
            Height = Ui.S(80);
            Margin = Ui.Pad(0, 0, 0, 2);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        public void SetSelected(bool value)
        {
            selected = value;
            Invalidate();
        }

        /// <summary>Az el nem küldött szöveg: az előnézet helyén "Piszkozat" jelzéssel látszik.</summary>
        public void SetDraft(string text)
        {
            text = text.Trim();
            if (draft == text) return;

            draft = text;
            Invalidate();
        }

        /// <summary>A beszélgetés adatai (előnézet, olvasatlan szám) megváltoztak: újrafestés.</summary>
        public void RefreshState()
        {
            UpdateAccessibleName();
            Invalidate();
        }

        private void UpdateAccessibleName() =>
            AccessibleName = Conversation.OtherUserName
                + (Conversation.UnreadCount > 0 ? $", {Conversation.UnreadCount} olvasatlan" : "")
                + (draft.Length > 0 ? ", piszkozat" : "")
                + (string.IsNullOrEmpty(Conversation.LastMessage) ? "" : ", " + Conversation.LastMessage);

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }

        internal static string ShortTime(DateTime? time)
        {
            if (time == null) return "";
            var local = time.Value.ToLocalTime();
            if (local.Date == DateTime.Today) return local.ToString("HH:mm", Ui.Hu);
            if (local.Date == DateTime.Today.AddDays(-1)) return "Tegnap";
            return local.ToString("MM. dd.", Ui.Hu);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var card = new Rectangle(Ui.S(4), 0, Width - Ui.S(8), Height - 1);
            Color fill = selected ? Theme.ChipBg : Anim.Lerp(Theme.CardBg, Theme.SoftHover, hoverT);
            using (var path = Ui.RoundedPath(card, Ui.S(14)))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            int avatarSize = Ui.S(48);
            var avatar = new Rectangle(card.X + Ui.S(14), (Height - avatarSize) / 2, avatarSize, avatarSize);
            Ui.DrawAvatar(g, avatar, Ui.Initials(Conversation.OtherUserName), Theme.MainBlue, Theme.White);

            int textX = avatar.Right + Ui.S(14);
            int right = card.Right - Ui.S(14);
            using var format = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            using var rightFormat = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Far };

            // idő jobb felül
            string time = ShortTime(Conversation.LastMessageTime);
            var timeFont = Ui.MakeFont(8.5F);
            int timeWidth = time.Length == 0 ? 0 : TextRenderer.MeasureText(time, timeFont).Width + Ui.S(4);
            using (var timeBrush = new SolidBrush(Theme.Muted))
                g.DrawString(time, timeFont, timeBrush, new Rectangle(right - timeWidth, Ui.S(14), timeWidth, Ui.S(20)), rightFormat);

            // név
            bool unread = Conversation.UnreadCount > 0;
            var nameFont = Ui.MakeFont(11F, FontStyle.Bold);
            using (var nameBrush = new SolidBrush(Theme.Text))
                g.DrawString(Conversation.OtherUserName, nameFont, nameBrush,
                    new Rectangle(textX, Ui.S(12), Math.Max(right - timeWidth - textX - Ui.S(6), 0), Ui.S(26)), format);

            // előnézet
            bool hasDraft = draft.Length > 0 && !selected;
            string preview = hasDraft
                ? "Piszkozat: " + draft
                : Conversation.LastMessageRetracted ? "Üzenet visszavonva" : (Conversation.LastMessage ?? "Még nincs üzenet");
            var previewFont = Ui.MakeFont(9.5F, unread ? FontStyle.Bold : FontStyle.Regular);
            int badgeSpace = unread ? Ui.S(34) : 0;
            using (var previewBrush = new SolidBrush(hasDraft ? Theme.Danger : unread ? Theme.Text : Theme.Muted))
                g.DrawString(preview.Replace('\n', ' '), previewFont, previewBrush,
                    new Rectangle(textX, Ui.S(44), Math.Max(right - textX - badgeSpace, 0), Ui.S(24)), format);

            // olvasatlan jelvény
            if (unread)
            {
                string count = Conversation.UnreadCount > 99 ? "99+" : Conversation.UnreadCount.ToString();
                var badgeFont = Ui.MakeFont(8.5F, FontStyle.Bold);
                int badgeWidth = Math.Max(Ui.S(24), TextRenderer.MeasureText(count, badgeFont).Width + Ui.S(12));
                var badge = new Rectangle(right - badgeWidth, Ui.S(44), badgeWidth, Ui.S(24));
                using (var badgePath = Ui.RoundedPath(badge, Ui.S(12)))
                using (var badgeBrush = new SolidBrush(Theme.MainBlue))
                    g.FillPath(badgeBrush, badgePath);
                using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var white = new SolidBrush(Theme.White);
                g.DrawString(count, badgeFont, white, badge, center);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Napi elválasztó az üzenetek között ("Ma", "Tegnap", dátum).</summary>
    internal sealed class DaySeparator : Control
    {
        private readonly string text;
        private readonly bool accent;

        public DaySeparator(string text, bool accent = false)
        {
            this.text = text;
            this.accent = accent;
            AccessibleName = text;
            AccessibleRole = AccessibleRole.Separator;
            Height = Ui.S(44);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var font = Ui.MakeFont(9F, FontStyle.Bold);
            var size = TextRenderer.MeasureText(text, font);
            int width = size.Width + Ui.S(28);
            int height = size.Height + Ui.S(10);
            var pill = new Rectangle((Width - width) / 2, (Height - height) / 2, width, height);

            using var path = Ui.RoundedPath(pill, height / 2);
            using var brush = new SolidBrush(accent ? Theme.MainBlue : Theme.ChipBg);
            g.FillPath(brush, path);
            using var textBrush = new SolidBrush(accent ? Theme.White : Theme.ChipFg);
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, pill, center);
        }
    }

    /// <summary>
    /// Üzenetbuborék: a saját üzenetek jobbra (kék), a másik félé balra. Szöveg, csatolmány, idő, "szerkesztve"
    /// és olvasottság-jelzés. Jobb gombbal: szerkesztés, visszavonás, másolás, hivatkozás megnyitása.
    /// A saját, még úton lévő üzenet halványan, a sikertelen pirosas kerettel látszik (kattintásra újra próbálja).
    /// </summary>
    internal sealed class MessageBubble : Control
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;

        public readonly MessageDto Message;
        private readonly bool mine;
        private readonly bool grouped;
        private readonly bool showText;
        private readonly Font textFont = Ui.MakeFont(10.5F);
        private readonly Font metaFont = Ui.MakeFont(8.5F);
        private readonly List<string> links;
        private ContextMenuStrip? menu;
        private double flashT;
        private Anim.Tween? flashTween;

        private Rectangle bubble;
        private Rectangle textRect;
        private Rectangle attachRect;
        private Rectangle openRect;
        private Rectangle metaRect;
        private string metaText = "";

        public event Action<MessageBubble>? EditRequested;
        public event Action<MessageBubble>? RetractRequested;
        public event Action<MessageBubble>? AttachmentRequested;
        public event Action<MessageBubble>? AttachmentOpenRequested;
        public event Action<MessageBubble>? RetryRequested;
        public event Action<MessageBubble>? DiscardRequested;

        /// <param name="grouped">Az előző üzenettel egy csoportba tartozik (azonos feladó, rövid időn belül): szorosabb térköz.</param>
        public MessageBubble(MessageDto message, bool mine, bool grouped = false)
        {
            Message = message;
            this.mine = mine;
            this.grouped = grouped;
            links = message.IsRetracted ? new List<string>() : ChatLogic.ExtractLinks(message.Content);

            string state = message.Send switch
            {
                SendState.Sending => " (küldés alatt)",
                SendState.Failed => " (nem sikerült elküldeni)",
                _ => ""
            };
            AccessibleName = (mine ? "Te" : "Másik fél") + ": "
                + (message.IsRetracted ? "visszavont üzenet" : message.HasAttachment ? "csatolmány: " + message.AttachmentName + " " + message.Content : message.Content)
                + state;
            AccessibleRole = AccessibleRole.StaticText;
            showText = !message.IsRetracted && !(message.HasAttachment && message.Content == message.AttachmentName) && message.Content.Length > 0;
            if (message.IsRetracted) textFont = Ui.MakeFont(10F, FontStyle.Italic);

            Margin = Ui.Pad(0);
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private string DisplayText => Message.IsRetracted ? "Üzenet visszavonva" : Message.Content;

        private bool Pending => Message.Send == SendState.Sending;
        private bool Failed => Message.Send == SendState.Failed;

        /// <summary>A sor szélességéhez igazítja a buborékot, és kiszámolja a magasságát.</summary>
        public void SetRowWidth(int rowWidth)
        {
            int padX = Ui.S(14);
            int padY = Ui.S(10);
            int maxContent = Math.Max((int)(rowWidth * 0.72) - padX * 2, Ui.S(120));

            var local = Message.CreatedAt.ToLocalTime();
            if (Pending)
            {
                metaText = "küldés…";
            }
            else if (Failed)
            {
                metaText = "Nem sikerült · újrapróbálás";
            }
            else
            {
                metaText = local.ToString("HH:mm", Ui.Hu);
                if (Message.EditedAt != null && !Message.IsRetracted) metaText += " · szerkesztve";
                if (mine && !Message.IsRetracted) metaText += Message.IsRead ? "  ✓✓" : "  ✓";
            }
            var metaSize = TextRenderer.MeasureText(metaText, metaFont);

            var textSize = Size.Empty;
            bool hasText = showText || Message.IsRetracted;
            if (hasText)
                textSize = TextRenderer.MeasureText(DisplayText, textFont, new Size(maxContent, 0), TextFlags);

            int attachWidth = 0;
            int attachHeight = Ui.S(44);
            bool hasAttachment = Message.HasAttachment && !Message.IsRetracted;
            if (hasAttachment)
            {
                var nameSize = TextRenderer.MeasureText(Message.AttachmentName ?? "", Ui.MakeFont(9.5F, FontStyle.Bold));
                // a név mellett a "Megnyitás" gomb is elfér
                attachWidth = Math.Min(maxContent, Math.Max(Ui.S(280), nameSize.Width + Ui.S(64) + Ui.S(100)));
            }

            int contentWidth = Math.Max(Math.Max(textSize.Width, attachWidth), metaSize.Width);
            int bubbleWidth = contentWidth + padX * 2;
            int y = padY;
            int textHeight = hasText ? textSize.Height : 0;

            int bubbleHeight = padY + textHeight;
            if (hasAttachment) bubbleHeight += (hasText ? Ui.S(8) : 0) + attachHeight;
            bubbleHeight += Ui.S(4) + metaSize.Height + padY;

            // az egy csoportba tartozó üzenetek szorosan követik egymást, a csoportok között nagyobb a köz
            int topGap = grouped ? 0 : Ui.S(6);
            int bubbleX = mine ? rowWidth - bubbleWidth - Ui.S(4) : Ui.S(4);
            bubble = new Rectangle(bubbleX, topGap, bubbleWidth, bubbleHeight);

            int cursor = bubble.Y + y;
            textRect = hasText ? new Rectangle(bubble.X + padX, cursor, contentWidth, textHeight) : Rectangle.Empty;
            if (hasText) cursor += textHeight;
            if (hasAttachment)
            {
                cursor += hasText ? Ui.S(8) : 0;
                attachRect = new Rectangle(bubble.X + padX, cursor, contentWidth, attachHeight);
                int openWidth = Ui.S(88);
                openRect = new Rectangle(attachRect.Right - openWidth - Ui.S(6), attachRect.Y + Ui.S(7), openWidth, attachRect.Height - Ui.S(14));
                cursor += attachHeight;
            }
            else
            {
                attachRect = Rectangle.Empty;
                openRect = Rectangle.Empty;
            }
            metaRect = new Rectangle(bubble.X + padX, cursor + Ui.S(4), contentWidth, metaSize.Height);

            Width = rowWidth;
            Height = bubble.Bottom + Ui.S(2);
            Invalidate();
        }

        /// <summary>Rövid, kiemelő villanás: a keresési találatra ugrás után ebből látszik, melyik üzenetről van szó.</summary>
        public void Flash()
        {
            flashTween?.Cancel();
            flashTween = Anim.Run(1600, t =>
            {
                flashT = 1 - t;
                Invalidate();
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            if (bubble.IsEmpty) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool retracted = Message.IsRetracted;
            bool filled = mine && !retracted && !Failed; // a kék, kitöltött "saját" megjelenés
            Color fill = filled ? (Pending ? Color.FromArgb(170, Theme.MainBlue) : Theme.MainBlue) : Theme.CardBg;
            Color textColor = filled ? Theme.White : (retracted ? Theme.Muted : Theme.Text);
            Color metaColor = Failed ? Theme.Danger : filled ? Color.FromArgb(200, Color.White) : Theme.Muted;

            if (flashT > 0.01)
            {
                using var glowPath = Ui.RoundedPath(Rectangle.Inflate(bubble, Ui.S(4), Ui.S(3)), Ui.S(19));
                using var glow = new SolidBrush(Color.FromArgb((int)(120 * flashT), Theme.Warning));
                g.FillPath(glow, glowPath);
            }

            using (var path = Ui.RoundedPath(bubble, Ui.S(16)))
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
                if (!filled)
                {
                    using var border = new Pen(Failed ? Theme.Danger : Theme.CardBorder);
                    g.DrawPath(border, path);
                }
            }

            if (!textRect.IsEmpty)
                TextRenderer.DrawText(g, DisplayText, textFont, textRect, textColor, TextFlags);

            if (!attachRect.IsEmpty)
                PaintAttachment(g, textColor);

            TextRenderer.DrawText(g, metaText, metaFont, metaRect, metaColor,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void PaintAttachment(Graphics g, Color textColor)
        {
            Color chip = mine ? Color.FromArgb(48, Color.White) : Theme.SoftHover;
            using (var path = Ui.RoundedPath(attachRect, Ui.S(10)))
            using (var brush = new SolidBrush(chip))
                g.FillPath(brush, path);

            var icon = new Rectangle(attachRect.X + Ui.S(8), attachRect.Y, Ui.S(32), attachRect.Height);
            using (var iconBrush = new SolidBrush(textColor))
            using (var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("\uE723", Ui.IconFont(14F), iconBrush, icon, center);

            // jobb oldalt a "Megnyitás" gomb; a név a maradék helyen jelenik meg
            using (var openPath = Ui.RoundedPath(openRect, openRect.Height / 2))
            {
                using var openFill = new SolidBrush(mine ? Color.FromArgb(70, Color.White) : Theme.ChipBg);
                g.FillPath(openFill, openPath);
            }
            TextRenderer.DrawText(g, "Megnyitás", Ui.MakeFont(8.5F, FontStyle.Bold), openRect, mine ? Color.White : Theme.ChipFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var nameRect = new Rectangle(icon.Right + Ui.S(4), attachRect.Y + Ui.S(5), Math.Max(openRect.Left - icon.Right - Ui.S(10), 0), Ui.S(20));
            TextRenderer.DrawText(g, Message.AttachmentName ?? "fájl", Ui.MakeFont(9.5F, FontStyle.Bold), nameRect, textColor,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            var sizeRect = new Rectangle(nameRect.X, nameRect.Bottom - Ui.S(1), nameRect.Width, Ui.S(18));
            TextRenderer.DrawText(g, FormatSize(Message.AttachmentSize), metaFont, sizeRect,
                mine ? Color.FromArgb(200, Color.White) : Theme.Muted, TextFormatFlags.NoPadding);
        }

        private static string FormatSize(long? bytes) => bytes == null ? "" : Ui.FormatBytes(bytes.Value);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool clickable = (!attachRect.IsEmpty && attachRect.Contains(e.Location)) || (Failed && bubble.Contains(e.Location));
            Cursor = clickable ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            if (Failed && bubble.Contains(e.Location))
            {
                RetryRequested?.Invoke(this);
                return;
            }

            if (attachRect.IsEmpty || !attachRect.Contains(e.Location)) return;

            // a "Megnyitás" gomb megnyitja, a kártya többi része csak elmenti a fájlt
            if (openRect.Contains(e.Location))
                AttachmentOpenRequested?.Invoke(this);
            else
                AttachmentRequested?.Invoke(this);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Right || Message.IsRetracted || Pending || !bubble.Contains(e.Location)) return;

            menu?.Dispose();
            menu = new ContextMenuStrip();

            if (Failed)
            {
                menu.Items.Add("Újrapróbálás", null, (_, _) => RetryRequested?.Invoke(this));
                menu.Items.Add("Elvetés", null, (_, _) => DiscardRequested?.Invoke(this));
                menu.Show(this, e.Location);
                return;
            }

            menu.Items.Add("Másolás", null, (_, _) =>
            {
                if (Message.Content.Length > 0) Clipboard.SetText(Message.Content);
            });
            foreach (string link in links)
            {
                string target = link;
                string shown = target.Length > 44 ? target[..43] + "…" : target;
                menu.Items.Add("Hivatkozás megnyitása: " + shown, null, (_, _) => LinkOpener.Open(target));
            }
            if (mine)
            {
                if (!Message.HasAttachment)
                    menu.Items.Add("Szerkesztés", null, (_, _) => EditRequested?.Invoke(this));
                menu.Items.Add("Visszavonás", null, (_, _) => RetractRequested?.Invoke(this));
            }
            menu.Show(this, e.Location);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                flashTween?.Cancel();
                menu?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Hivatkozás megnyitása az alapértelmezett böngészőben (csak http / https).</summary>
    internal static class LinkOpener
    {
        public static void Open(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                MessageBox.Show("A hivatkozást nem sikerült megnyitni: " + ex.Message, "Hivatkozás", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>Egy találat az üzenetek közötti keresésben: a beszélgetés neve, az üzenet kelte és a kiemelt részlet.</summary>
    internal sealed class MessageHitItem : Control
    {
        public readonly MessageHit Hit;
        private double hoverT;
        private Anim.Tween? hoverTween;

        public MessageHitItem(MessageHit hit)
        {
            Hit = hit;
            AccessibleName = $"{hit.Conversation.OtherUserName}: {hit.Snippet}";
            AccessibleRole = AccessibleRole.ListItem;
            Height = Ui.S(78);
            Margin = Ui.Pad(0, 0, 0, 2);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
        }

        private void HoverTo(double to)
        {
            hoverTween?.Cancel();
            double from = hoverT;
            hoverTween = Anim.Run(140, t =>
            {
                hoverT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnMouseEnter(EventArgs e) { HoverTo(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { HoverTo(0); base.OnMouseLeave(e); }

        private static string DateText(DateTime time)
        {
            var local = time.ToLocalTime();
            if (local.Date == DateTime.Today || local.Date == DateTime.Today.AddDays(-1))
                return ConversationItem.ShortTime(time);
            return local.ToString("yyyy. MM. dd.", Ui.Hu);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var card = new Rectangle(Ui.S(4), 0, Width - Ui.S(8), Height - 1);
            using (var path = Ui.RoundedPath(card, Ui.S(14)))
            using (var brush = new SolidBrush(Anim.Lerp(Theme.CardBg, Theme.SoftHover, hoverT)))
                g.FillPath(brush, path);

            int pad = Ui.S(16);
            int left = card.X + pad;
            int right = card.Right - pad;

            // fent: a beszélgetés neve, jobbra az üzenet kelte
            var dateFont = Ui.MakeFont(8.5F);
            string date = DateText(Hit.Message.CreatedAt);
            int dateWidth = TextRenderer.MeasureText(date, dateFont).Width + Ui.S(4);
            TextRenderer.DrawText(g, date, dateFont, new Rectangle(right - dateWidth, Ui.S(14), dateWidth, Ui.S(20)), Theme.Muted,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, Hit.Conversation.OtherUserName, Ui.MakeFont(10.5F, FontStyle.Bold),
                new Rectangle(left, Ui.S(12), Math.Max(right - dateWidth - left - Ui.S(8), 0), Ui.S(24)), Theme.Text,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

            // lent: a találat részlete, a keresett szöveg kiemelve
            var normal = Ui.MakeFont(9.5F);
            var bold = Ui.MakeFont(9.5F, FontStyle.Bold);
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            var textArea = new Rectangle(left, Ui.S(44), Math.Max(right - left, 0), Ui.S(24));

            string snippet = Hit.Snippet;
            int start = Math.Clamp(Hit.MatchStart, 0, snippet.Length);
            int length = Math.Clamp(Hit.MatchLength, 0, snippet.Length - start);
            string before = snippet[..start];
            string match = snippet.Substring(start, length);
            string after = snippet[(start + length)..];

            var oldClip = g.Clip;
            g.SetClip(textArea);
            int x = textArea.X;
            foreach (var (text, font, color) in new[] { (before, normal, Theme.Muted), (match, bold, Theme.MainBlue), (after, normal, Theme.Muted) })
            {
                if (text.Length == 0) continue;
                int width = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, textArea.Height), flags).Width;
                TextRenderer.DrawText(g, text, font, new Rectangle(x, textArea.Y, width + Ui.S(2), textArea.Height), color, flags);
                x += width;
                if (x >= textArea.Right) break;
            }
            g.Clip = oldClip;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hoverTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Üzenetíró mező: lekerekített "pirula". Enterre küld, Shift+Enterrel új sort kezd; hosszabb szövegnél
    /// felfelé nő (legfeljebb <see cref="MaxLines"/> sorig), a Chat oldal ehhez igazítja a sáv magasságát.
    /// </summary>
    internal sealed class ComposerBox : Control
    {
        public const int MaxLines = 5;

        private readonly TextBox input;
        private double focusT;
        private Anim.Tween? focusTween;
        private int lines = 1;

        public event EventHandler? SendRequested;
        public event EventHandler? TextEdited;

        /// <summary>A szöveg sorainak száma megváltozott: a kívánt magasság más lett.</summary>
        public event EventHandler? PreferredHeightChanged;

        /// <summary>A vágólapról beillesztett (Ctrl+V) kép vagy fájl útvonala; a kép ideiglenes PNG-fájlként készül el.</summary>
        public event Action<string>? FileDropped;

        public ComposerBox(string placeholder)
        {
            Cursor = Cursors.IBeam;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Ui.MakeFont(11F),
                PlaceholderText = placeholder,
                BackColor = Theme.InputBg,
                ForeColor = Theme.Text,
                MaxLength = 2000,
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = true,
                ScrollBars = ScrollBars.None,
                AccessibleName = "Üzenet szövege (Enter: küldés, Shift+Enter: új sor)"
            };
            input.GotFocus += (_, _) => FocusTo(1);
            input.LostFocus += (_, _) => FocusTo(0);
            input.TextChanged += (_, _) =>
            {
                UpdateLines();
                TextEdited?.Invoke(this, EventArgs.Empty);
            };
            input.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true; // ne csipogjon, és ne kerüljön új sor a szövegbe
                    SendRequested?.Invoke(this, EventArgs.Empty);
                }
                else if (e.Control && e.KeyCode == Keys.V && TryGetClipboardFile() is { } path)
                {
                    // fájl vagy kép van a vágólapon (nem szöveg): küldésre ajánljuk fel
                    e.SuppressKeyPress = true;
                    e.Handled = true;
                    FileDropped?.Invoke(path);
                }
            };
            Controls.Add(input);
        }

        /// <summary>A küldendő szöveg: a sortörések egységesen "\n"-nel, a szélső szóközök nélkül.</summary>
        public string Value => input.Text.Replace("\r\n", "\n").Trim();

        /// <summary>Beállítja a szöveget (a "\n" sortörések a mezőben "\r\n"-ként jelennek meg).</summary>
        public void SetValue(string text) => input.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");

        /// <summary>A mező kívánt magassága a sorok számához igazítva (egy sornál 50 képpont, 100%-os nagyításon).</summary>
        public int PreferredHeight => Math.Max(Ui.S(50), lines * Ui.LineHeight(input.Font) + Ui.S(22));

        private void UpdateLines()
        {
            string text = input.Text.Length == 0 ? " " : input.Text;
            if (text.EndsWith('\n')) text += " "; // a záró üres sor is számít

            int width = Math.Max(input.ClientSize.Width, 40);
            int height = TextRenderer.MeasureText(text, input.Font, new Size(width, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl).Height;
            int count = Math.Clamp((int)Math.Ceiling(height / (double)Ui.LineHeight(input.Font)), 1, MaxLines);
            if (count == lines) return;

            lines = count;
            PreferredHeightChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Szöveg beszúrása a kurzor helyére (pl. emoji), majd a fókusz visszakerül a mezőbe.</summary>
        public void InsertText(string text)
        {
            if (input.TextLength + text.Length > input.MaxLength) return;
            input.SelectedText = text;
            input.Focus();
        }

        /// <summary>A szövegmezőre húzott fájlok kezelése (a mező maga is fogadja a húzást).</summary>
        public void AttachDrop(DragEventHandler enter, DragEventHandler drop)
        {
            AllowDrop = true;
            input.AllowDrop = true;
            DragEnter += enter;
            DragDrop += drop;
            input.DragEnter += enter;
            input.DragDrop += drop;
        }

        /// <summary>A vágólap fájlja vagy képe (csak ha nincs rajta szöveg); null, ha nincs ilyen.</summary>
        private static string? TryGetClipboardFile()
        {
            try
            {
                if (Clipboard.ContainsText()) return null;

                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count > 0) return files[0];
                }

                if (Clipboard.ContainsImage())
                {
                    using var image = Clipboard.GetImage();
                    if (image == null) return null;

                    string folder = Path.Combine(Path.GetTempPath(), "TutorConnect", "paste");
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, $"kep-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                    image.Save(path, ImageFormat.Png);
                    return path;
                }
            }
            catch (Exception ex) when (ex is ExternalException or IOException or UnauthorizedAccessException)
            {
                // a vágólap éppen foglalt vagy a kép nem menthető: a szokásos beillesztés marad
            }
            return null;
        }

        public void FocusInput() => input.Focus();

        private void FocusTo(double to)
        {
            focusTween?.Cancel();
            double from = focusT;
            focusTween = Anim.Run(160, t =>
            {
                focusT = from + (to - from) * t;
                Invalidate();
            });
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (input is null) return;

            input.Left = Ui.S(20);
            input.Width = Math.Max(Width - Ui.S(40), 20);
            input.Height = Math.Max(Math.Min(lines * Ui.LineHeight(input.Font) + 2, Height - Ui.S(12)), 10);
            input.Top = (Height - input.Height) / 2;
            UpdateLines(); // a szélesség változása a sortörést is módosítja
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            input.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Ui.EffectiveBack(Parent));
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = Ui.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Math.Min(Height / 2, Ui.S(25)));
            using var fill = new SolidBrush(Theme.InputBg);
            using var border = new Pen(Anim.Lerp(Theme.CardBorder, Theme.MainBlue, focusT), 1F + (float)focusT);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) focusTween?.Cancel();
            base.Dispose(disposing);
        }
    }

    /// <summary>Saját üzenet szövegének szerkesztése.</summary>
    internal sealed class EditMessageDialog : DialogBase
    {
        private readonly TextBox box;

        public string NewText { get; private set; } = "";

        public EditMessageDialog(string text)
            : base("Üzenet szerkesztése", "A módosított üzenet mellett \"szerkesztve\" jelzés jelenik meg.", "Mentés", Ui.Sz(560, 400), "\uE70F")
        {
            Section("Üzenet szövege");
            box = NewTextBox(170, 2000);
            box.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            AddBody(box);
            SetFooter("");
        }

        protected override Task OnActionAsync()
        {
            string text = box.Text.Replace("\r\n", "\n").Trim();
            if (text.Length == 0)
            {
                SetFooter("Az üzenet nem lehet üres.", error: true);
                return Task.CompletedTask;
            }

            NewText = text;
            DialogResult = DialogResult.OK;
            Close();
            return Task.CompletedTask;
        }
    }
}
