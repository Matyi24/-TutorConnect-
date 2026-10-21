namespace TutorConnectApp
{
    // Üzenetek megjelenítése: megnyitás gyorsítótárból, frissítés változtatásonként, görgetés
    internal sealed partial class ChatPage
    {
        private List<MessageDto> messages = new();
        private int shownFrom;
        private bool messagesLoaded;
        private int lastMessageWidth = -1;

        /// <summary>Megnyitás után erre az üzenetre görget (keresési találat), ha van.</summary>
        private int? scrollToMessageId;

        /// <summary>Az első olvasatlan üzenet: előtte "Új üzenetek" elválasztó látszik.</summary>
        private int? unreadMarkerId;
        private DaySeparator? unreadSeparator;

        /// <summary>A megjelenített buborékok üzenetazonosító szerint (gyors keresés és célzott csere).</summary>
        private readonly Dictionary<int, MessageBubble> bubbles = new();

        /// <summary>Az üzenet állapota a kirajzoláskor: ebből derül ki, kell-e a buborékot újrarajzolni.</summary>
        private readonly Dictionary<int, string> renderedSigs = new();

        // ------------------------------------------------------------
        // Megnyitás és letöltés
        // ------------------------------------------------------------

        private async Task SelectAsync(ConversationDto conversation)
        {
            bool same = current?.Id == conversation.Id;
            if (!same) StoreMessages();

            current = conversation;
            showThread = true;
            int unread = conversation.UnreadCount; // az olvasottnak jelölés előtt: ebből látszik, hol kezdődnek az új üzenetek

            foreach (var item in listScroll.Content.Controls.OfType<ConversationItem>())
                item.SetSelected(item.Conversation.Id == conversation.Id);

            headerName.Text = conversation.OtherUserName;
            headerStatus.Text = "";
            profileButton.Visible = true;
            placeholder.Visible = false;
            threadHeader.Visible = true;
            composerBar.Visible = true;
            messageScroll.Visible = true;
            UpdateLayout();
            LayoutComposer();

            // az el nem küldött szöveg visszatöltése (a beállítás közben nem számít szerkesztésnek)
            restoringDraft = true;
            composer.SetValue(DraftFor(conversation.Id));
            restoringDraft = false;

            bool shown = same && messagesLoaded;
            if (!same)
            {
                // a másik beszélgetés üzenetei ne maradjanak az új fejléc alatt
                messagesLoaded = false;
                messages = new List<MessageDto>();
                unreadMarkerId = null;
                ClearMessageView();

                // már ismert beszélgetés: az üzenetek azonnal látszanak, a frissek a háttérben érkeznek
                if (ChatMessages.TryGet(conversation.Id, out var cached))
                {
                    messages = new List<MessageDto>(cached);
                    messagesLoaded = true;
                    PrepareUnreadMarker(unread);
                    shownFrom = Math.Max(0, messages.Count - MessagePageSize);
                    RenderMessages();
                    ScrollAfterOpen();
                    shown = true;
                }
            }

            if (!shown) ShowStatus("Üzenetek betöltése...", error: false, autoHide: false);

            await FetchMessagesAsync(conversation, firstLoad: !shown, scrollToEnd: false, unread);
            if (IsDisposed || current?.Id != conversation.Id) return;

            composer.FocusInput();
            await MarkReadAsync();
        }

        /// <summary>
        /// Letölti a beszélgetés üzeneteit, és a megjelenítettet a változásokhoz igazítja (csak ami módosult, az rajzolódik újra).
        /// Igaz, ha a kijelzett tartalom megváltozott.
        /// </summary>
        private async Task<bool> FetchMessagesAsync(ConversationDto open, bool firstLoad, bool scrollToEnd, int unread = 0, bool quiet = false)
        {
            try
            {
                var fresh = await ApiClient.GetAsync<List<MessageDto>>($"/api/conversations/{open.Id}/messages");
                if (IsDisposed || current?.Id != open.Id) return false;

                // a letöltés alatt érkezett, illetve a még úton lévő üzenetek nem veszhetnek el
                var merged = ChatLogic.Merge(fresh, messages);
                ChatMessages.Set(open.Id, merged);

                if (firstLoad)
                {
                    messages = merged;
                    messagesLoaded = true;
                    PrepareUnreadMarker(unread);
                    shownFrom = Math.Max(0, messages.Count - MessagePageSize);
                    ShowStatus("", error: false);
                    RenderMessages();
                    ScrollAfterOpen();
                    return true;
                }

                messagesLoaded = true;
                return ShowMessageList(merged, scrollToEnd);
            }
            catch (ApiException ex)
            {
                if (!quiet && !IsDisposed && current?.Id == open.Id) ShowStatus(ex.Message, error: true);
                return false;
            }
        }

        /// <summary>A megnyitott beszélgetés újratöltése (pl. szerkesztés eseményére).</summary>
        private async Task ReloadMessagesAsync(bool scrollToEnd)
        {
            var open = current;
            if (open != null) await FetchMessagesAsync(open, firstLoad: false, scrollToEnd);
        }

        /// <summary>A beszélgetés üzenetei a gyorsítótárba kerülnek (másik beszélgetésre váltáskor, újraépítéskor).</summary>
        private void StoreMessages()
        {
            if (current != null && messagesLoaded)
                ChatMessages.Set(current.Id, messages);
        }

        private void PrepareUnreadMarker(int unread)
        {
            unreadMarkerId = null;
            if (unread <= 0) return;

            int index = ChatLogic.FirstUnreadIndex(messages, me.Id);
            if (index >= 0) unreadMarkerId = messages[index].Id;
        }

        // ------------------------------------------------------------
        // Megjelenítés
        // ------------------------------------------------------------

        private int RowWidth()
        {
            var content = messageScroll.Content;
            return Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(160));
        }

        private string Signature(MessageDto m) =>
            $"{(m.SenderId == me.Id && m.IsRead ? 1 : 0)}|{(m.IsRetracted ? 1 : 0)}|{m.EditedAt?.Ticks}|{(int)m.Send}|{m.Content.Length}|{m.AttachmentName}";

        private void ClearMessageView()
        {
            Ui.Reset(messageScroll.Content);
            bubbles.Clear();
            renderedSigs.Clear();
            unreadSeparator = null;
            UpdateScrollDownButton();
        }

        /// <summary>Az egész megjelenített szakasz újraépítése (megnyitáskor, régebbi üzenetek betöltésekor).</summary>
        private void RenderMessages()
        {
            var content = messageScroll.Content;
            content.SuspendLayout();
            ClearMessageView();

            // régebbi üzenetek: csak kérésre jelennek meg, így hosszú beszélgetésnél sem lassul a felület
            if (shownFrom > 0)
            {
                var older = Ui.Btn($"Korábbi üzenetek ({shownFrom})", Theme.CardBg, Theme.Text, (_, _) => BeginInvoke(new Action(ShowOlderMessages)), outline: true);
                older.Tag = "older";
                content.Controls.Add(older);
            }

            DateTime? lastDay = null;
            for (int i = shownFrom; i < messages.Count; i++)
                AddMessageControl(messages[i], i > 0 ? messages[i - 1] : null, ref lastDay);

            FitMessages(); // még felfüggesztett elrendezésnél: egyszer számolódik újra
            content.ResumeLayout(true);
            UpdateScrollDownButton();
        }

        /// <summary>
        /// Az új lista alapján frissíti a kijelzőt. Ha csak a végére kerültek új üzenetek, illetve egyes üzenetek állapota
        /// változott (olvasott, szerkesztett, visszavont), csak azok rajzolódnak újra; egyébként az egész újraépül.
        /// </summary>
        private bool ShowMessageList(List<MessageDto> list, bool scrollToEnd)
        {
            var previous = messages;
            messages = list;

            bool prefixSame = list.Count >= previous.Count;
            for (int i = 0; prefixSame && i < previous.Count; i++)
                prefixSame = list[i].Id == previous[i].Id;

            if (!prefixSame)
            {
                shownFrom = Math.Min(shownFrom, Math.Max(0, messages.Count - MessagePageSize));
                RenderMessages();
                if (scrollToEnd) ScrollToEnd(animate: false);
                return true;
            }

            bool changed = false;
            bool stick = scrollToEnd || messageScroll.IsNearEnd();

            for (int i = shownFrom; i < previous.Count; i++)
            {
                var message = list[i];
                if (renderedSigs.TryGetValue(message.Id, out var sig) && sig == Signature(message)) continue;

                ReplaceBubble(message.Id, message);
                changed = true;
            }

            for (int i = previous.Count; i < list.Count; i++)
            {
                AddMessageAt(i);
                changed = true;
            }

            if (changed)
            {
                if (stick) ScrollToEnd(animate: false);
                else UpdateScrollDownButton();
            }
            return changed;
        }

        /// <summary>Még egy oldalnyi régebbi üzenet; az olvasott rész a képernyőn ugyanott marad.</summary>
        private void ShowOlderMessages()
        {
            double offset = messageScroll.Offset;
            int oldHeight = messageScroll.Content.Height;

            shownFrom = Math.Max(0, shownFrom - MessagePageSize);
            RenderMessages();

            messageScroll.JumpTo(offset + (messageScroll.Content.Height - oldHeight));
        }

        private MessageBubble CreateBubble(MessageDto message, bool grouped)
        {
            var bubble = new MessageBubble(message, message.SenderId == me.Id, grouped);
            bubble.EditRequested += async b => await EditAsync(b.Message);
            bubble.RetractRequested += async b => await RetractAsync(b.Message);
            bubble.AttachmentRequested += async b => await OpenAttachmentAsync(b.Message, open: false);
            bubble.AttachmentOpenRequested += async b => await OpenAttachmentAsync(b.Message, open: true);
            bubble.RetryRequested += async b => await RetryAsync(b.Message);
            bubble.DiscardRequested += b => DiscardFailed(b.Message);
            return bubble;
        }

        private void AddMessageControl(MessageDto message, MessageDto? previous, ref DateTime? lastDay)
        {
            var content = messageScroll.Content;

            var day = message.CreatedAt.ToLocalTime().Date;
            if (lastDay != day)
            {
                content.Controls.Add(new DaySeparator(ChatLogic.DayLabel(day, DateTime.Today)));
                lastDay = day;
            }

            if (unreadMarkerId == message.Id && unreadSeparator == null)
            {
                unreadSeparator = new DaySeparator("Új üzenetek", accent: true);
                content.Controls.Add(unreadSeparator);
            }

            var bubble = CreateBubble(message, ChatLogic.IsGrouped(previous, message));
            content.Controls.Add(bubble);
            bubbles[message.Id] = bubble;
            renderedSigs[message.Id] = Signature(message);
        }

        /// <summary>A lista adott elemét a kijelző végéhez fűzi (nap-elválasztóval, ha új nap kezdődik).</summary>
        private void AddMessageAt(int index)
        {
            var content = messageScroll.Content;
            int start = content.Controls.Count;

            var before = index > 0 ? messages[index - 1] : null;
            DateTime? lastDay = before?.CreatedAt.ToLocalTime().Date;
            AddMessageControl(messages[index], before, ref lastDay);
            FitFrom(start);
        }

        /// <summary>
        /// Egyetlen üzenet buborékját cseréli le (szerkesztés, visszavonás, olvasottság, küldés eredménye után),
        /// az egész lista újraépítése nélkül. Az oldId a régi buborék azonosítója (küldésnél az ideiglenes).
        /// </summary>
        private void ReplaceBubble(int oldId, MessageDto message)
        {
            if (!bubbles.TryGetValue(oldId, out var old)) return;

            var content = messageScroll.Content;
            int index = content.Controls.GetChildIndex(old);
            int at = messages.FindIndex(m => m.Id == message.Id);
            var before = at > 0 ? messages[at - 1] : null;
            var replacement = CreateBubble(message, ChatLogic.IsGrouped(before, message));

            content.SuspendLayout();
            content.Controls.Add(replacement);
            content.Controls.SetChildIndex(replacement, index);
            content.Controls.Remove(old);
            old.Dispose();
            replacement.SetRowWidth(RowWidth());
            content.ResumeLayout(true);

            bubbles.Remove(oldId);
            bubbles[message.Id] = replacement;
            renderedSigs.Remove(oldId);
            renderedSigs[message.Id] = Signature(message);
        }

        private void ReplaceBubble(MessageDto message) => ReplaceBubble(message.Id, message);

        private void RemoveMessage(MessageDto message)
        {
            messages.Remove(message);
            renderedSigs.Remove(message.Id);

            if (bubbles.Remove(message.Id, out var bubble))
            {
                messageScroll.Content.Controls.Remove(bubble);
                bubble.Dispose();
            }
            UpdateScrollDownButton();
        }

        /// <summary>A beszélgetés előnézetét igazítja, ha a módosított üzenet az utolsó volt.</summary>
        private void RefreshPreviewIfLast(MessageDto message)
        {
            var open = current;
            if (open == null || messages.Count == 0 || messages[^1].Id != message.Id) return;
            ChatState.UpdatePreview(open, message.IsRetracted ? "" : message.Content, message.IsRetracted);
        }

        private void AppendMessage(MessageDto message, bool forceStick = false)
        {
            if (messages.Any(m => m.Id == message.Id)) return;

            bool stick = forceStick || message.SenderId == me.Id || messageScroll.IsNearEnd();
            messages.Add(message);
            AddMessageAt(messages.Count - 1);

            if (stick) ScrollToEnd();
            else UpdateScrollDownButton();
        }

        private void FitMessages() => FitFrom(0);

        /// <summary>A megadott sorszámtól kezdve a sor szélességéhez igazítja a vezérlőket.</summary>
        private void FitFrom(int start)
        {
            var content = messageScroll.Content;
            int width = RowWidth();

            content.SuspendLayout();
            for (int i = start; i < content.Controls.Count; i++)
            {
                switch (content.Controls[i])
                {
                    case MessageBubble bubble:
                        bubble.SetRowWidth(width);
                        break;
                    case DaySeparator separator:
                        separator.Width = width;
                        break;
                    case HoverButton { Tag: "older" } older:
                        older.Margin = new Padding(Math.Max((width - older.Width) / 2, 0), Ui.S(6), 0, Ui.S(10));
                        break;
                }
            }
            content.ResumeLayout(true);
        }

        // ------------------------------------------------------------
        // Görgetés
        // ------------------------------------------------------------

        private void ScrollToEnd(bool animate = true)
        {
            if (!animate)
            {
                // azonnali ugrás; egy második, késleltetett ugrás a később beálló magasság miatt
                messageScroll.JumpTo(double.MaxValue);
                BeginInvoke(new Action(() => messageScroll.JumpTo(double.MaxValue)));
                return;
            }

            // a magasság a következő elrendezés után áll be, ezért késleltetve görgetünk
            BeginInvoke(new Action(() => messageScroll.ScrollBy(1e9)));
        }

        /// <summary>Megnyitáskor: a keresett üzenethez, az első olvasatlanhoz, egyébként a legutolsó üzenethez ugrik.</summary>
        private void ScrollAfterOpen()
        {
            int? target = scrollToMessageId;
            scrollToMessageId = null;
            if (target is int id && ScrollToMessage(id)) return;

            var separator = unreadSeparator;
            if (separator != null)
            {
                void Jump()
                {
                    if (!IsDisposed && !separator.IsDisposed)
                        messageScroll.JumpTo(Math.Max(separator.Top - Ui.S(24), 0));
                }

                Jump();
                BeginInvoke(new Action(Jump));
                return;
            }

            ScrollToEnd(animate: false);
        }

        /// <summary>A megadott üzenethez görget (ha kell, előtte betölti a régebbi üzeneteket), és kiemeli.</summary>
        private bool ScrollToMessage(int messageId)
        {
            int index = messages.FindIndex(m => m.Id == messageId);
            if (index < 0) return false;

            if (index < shownFrom)
            {
                shownFrom = Math.Max(0, index - 5);
                RenderMessages();
            }

            if (!bubbles.TryGetValue(messageId, out var bubble)) return false;

            void Jump()
            {
                if (!IsDisposed && !bubble.IsDisposed)
                    messageScroll.JumpTo(Math.Max(bubble.Top - Ui.S(90), 0));
            }

            Jump();
            BeginInvoke(new Action(Jump));
            bubble.Flash();
            return true;
        }
    }
}
