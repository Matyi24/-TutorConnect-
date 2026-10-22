namespace TutorConnectApp
{
    // Beszélgetéslista és keresés az üzenetek között
    internal sealed partial class ChatPage
    {
        private string listFilter = "";
        private List<MessageHit> hits = new();
        private string? searchStatus;
        private int searchSeq;

        /// <summary>
        /// Kilépés a keresésből: a keresőmező és a találatok törlődnek, a lista újra a teljes beszélgetés-lista,
        /// a kurzor pedig a megnyitott beszélgetés írómezőjébe (vagy sehová) kerül.
        /// </summary>
        private void ExitSearch()
        {
            if (IsDisposed) return;

            bool had = listSearch.Value.Length > 0 || hits.Count > 0 || searchStatus != null;
            searchSeq++; // a még futó keresés eredményét eldobjuk
            listFilter = "";
            hits = new List<MessageHit>();
            searchStatus = null;
            if (listSearch.Value.Length > 0) listSearch.Clear();
            if (had) RenderList();

            if (listSearch.InputFocused)
            {
                if (current != null && ThreadVisible) composer.FocusInput();
                else FindForm()?.Focus();
            }
        }

        /// <summary>
        /// Figyeli az egérkattintásokat: ha a keresőn és a találatokon kívülre kattintasz, a keresés véget ér.
        /// A kattintás előbb a saját célpontjára hat (pl. beszélgetés megnyitása), a kilépés csak utána fut le.
        /// </summary>
        private sealed class SearchClickFilter : IMessageFilter
        {
            private const int WM_LBUTTONUP = 0x0202;
            private readonly ChatPage page;

            public SearchClickFilter(ChatPage page) => this.page = page;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_LBUTTONUP || page.IsDisposed || !page.IsHandleCreated) return false;
                if (page.listSearch.Value.Length == 0 && page.hits.Count == 0 && page.searchStatus == null) return false;

                var target = Control.FromHandle(m.HWnd);
                if (target == null || target.FindForm() != page.FindForm()) return false; // másik ablak (pl. dialógus)

                // a kereső saját területe és a találatok nem zárják be a keresést
                for (Control? c = target; c != null; c = c.Parent)
                {
                    if (ReferenceEquals(c, page.listSearch) || c is MessageHitItem) return false;
                }

                page.BeginInvoke(new Action(page.ExitSearch));
                return false;
            }
        }

        /// <summary>A keresőmező változása (a gépelés szünete után): a lista szűrése, majd keresés az üzenetek szövegében is.</summary>
        private void OnFilterChanged()
        {
            if (IsDisposed) return;

            int seq = ++searchSeq; // a korábbi, még futó keresés eredményét eldobjuk
            hits = new List<MessageHit>();
            searchStatus = null;
            RenderList();

            if (listFilter.Length >= 2)
                _ = SearchMessagesAsync(listFilter, seq);
        }

        /// <summary>
        /// Az összes beszélgetés üzeneteiben keres. Ami még nincs letöltve, azt legfeljebb három párhuzamos kéréssel hozza le
        /// (és el is tárolja, így a beszélgetés későbbi megnyitása is azonnali).
        /// </summary>
        private async Task SearchMessagesAsync(string query, int seq)
        {
            var conversations = ChatState.Conversations.ToList();
            var loaded = new Dictionary<int, List<MessageDto>>();
            var missing = new List<ConversationDto>();

            foreach (var conversation in conversations)
            {
                if (ChatMessages.TryGet(conversation.Id, out var cached)) loaded[conversation.Id] = cached;
                else missing.Add(conversation);
            }

            if (missing.Count > 0)
            {
                searchStatus = "Keresés az üzenetekben…";
                RenderList();

                using var gate = new SemaphoreSlim(3);
                await Task.WhenAll(missing.Select(async conversation =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        if (IsDisposed || seq != searchSeq) return;
                        var list = await ChatMessages.GetOrLoadAsync(conversation.Id);
                        lock (loaded) loaded[conversation.Id] = list;
                    }
                    catch (ApiException)
                    {
                        // egy beszélgetés letöltése nem sikerült: a többiben tovább keresünk
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }

            if (IsDisposed || seq != searchSeq) return;

            var source = conversations
                .Where(c => loaded.ContainsKey(c.Id))
                .Select(c => (Conversation: c, Messages: (IReadOnlyList<MessageDto>)loaded[c.Id]));
            hits = ChatLogic.Search(source, query);
            searchStatus = null;
            RenderList();
        }

        /// <summary>Találatra kattintás: megnyitja a beszélgetést, és az üzenethez görget.</summary>
        private async Task OpenHitAsync(MessageHit hit)
        {
            // a találat a keresés idejéből való példányt tárolja: a lista közben cserélődhetett
            var conversation = ChatState.Find(hit.Conversation.Id) ?? hit.Conversation;

            if (current?.Id == conversation.Id)
            {
                showThread = true;
                UpdateLayout();
                ScrollToMessage(hit.Message.Id);
                return;
            }

            scrollToMessageId = hit.Message.Id;
            await SelectAsync(conversation);
        }

        public override bool FocusSearch()
        {
            if (!Wide && showThread) BackToList(); // keskeny ablaknál a lista mögött van a keresőmező
            listSearch.FocusInput();
            return true;
        }

        private string DraftFor(int conversationId) => Drafts.TryGetValue(conversationId, out var text) ? text : "";

        private void RenderList()
        {
            if (IsDisposed) return;

            var content = listScroll.Content;

            // ugyanaz a lista (azonos objektumok, azonos sorrend): csak újrafestjük, nem építjük újra az elemeket
            var filtered = FilteredConversations();
            var existing = content.Controls.OfType<ConversationItem>().ToList();
            bool plain = hits.Count == 0 && searchStatus == null;
            if (plain && filtered.Count > 0 && existing.Count == filtered.Count && content.Controls.Count == existing.Count
                && existing.Zip(filtered).All(pair => ReferenceEquals(pair.First.Conversation, pair.Second)))
            {
                foreach (var item in existing)
                {
                    item.SetSelected(current?.Id == item.Conversation.Id);
                    item.SetDraft(DraftFor(item.Conversation.Id));
                    item.RefreshState();
                }
                return;
            }

            content.SuspendLayout();

            // az üzenet-címke nem szabad, hogy a Reset-tel megsemmisüljön
            if (content.Controls.Contains(listMessage)) content.Controls.Remove(listMessage);
            Ui.Reset(content);

            if (filtered.Count == 0 && plain)
            {
                listMessage.Text = ChatState.Conversations.Count > 0
                    ? "Nincs a keresésnek megfelelő beszélgetés."
                    : me.Role == "STUDENT"
                        ? "Még nincs beszélgetésed. A + Új gombbal indíthatsz egyet."
                        : "Még nem érkezett üzeneted. Ha egy diák ír neked, itt fog megjelenni.";
                content.Controls.Add(listMessage);
            }
            else
            {
                foreach (var conversation in filtered)
                {
                    var item = new ConversationItem(conversation);
                    item.SetSelected(current?.Id == conversation.Id);
                    item.SetDraft(DraftFor(conversation.Id));
                    item.Click += async (_, _) => await SelectAsync(conversation);
                    content.Controls.Add(item);
                }

                if (searchStatus != null || hits.Count > 0)
                {
                    string heading = searchStatus ?? $"Találatok az üzenetekben ({hits.Count})";
                    content.Controls.Add(Ui.Lbl(heading, 9.5F, Theme.Muted, bold: true, margin: Ui.Pad(16, 14, 8, 8)));

                    foreach (var hit in hits)
                    {
                        var item = new MessageHitItem(hit);
                        var captured = hit;
                        item.Click += async (_, _) => await OpenHitAsync(captured);
                        content.Controls.Add(item);
                    }

                    if (searchStatus == null && hits.Count == 0 && filtered.Count == 0)
                        content.Controls.Add(Ui.Lbl("Nincs találat.", 10F, Theme.Muted, margin: Ui.Pad(16, 8, 8, 8)));
                }
                FitList();
            }

            content.ResumeLayout(true);
        }

        private List<ConversationDto> FilteredConversations()
        {
            var all = ChatState.Conversations;
            if (listFilter.Length == 0) return all.ToList();

            return all.Where(c => TextSearch.Contains(c.OtherUserName, listFilter) || TextSearch.Contains(c.LastMessage ?? "", listFilter)).ToList();
        }

        private void FitList()
        {
            var content = listScroll.Content;
            int width = Math.Max(content.ClientSize.Width - content.Padding.Horizontal, Ui.S(120));

            content.SuspendLayout();
            foreach (Control control in content.Controls)
            {
                if (control is (ConversationItem or MessageHitItem) && control.Width != width) control.Width = width;
            }
            content.ResumeLayout(true);
        }

        /// <summary>A megnyitott beszélgetés piszkozat-jelzése a listában (gépelés közben frissül).</summary>
        private void RefreshDraftBadge()
        {
            if (current == null) return;

            foreach (var item in listScroll.Content.Controls.OfType<ConversationItem>())
            {
                if (item.Conversation.Id == current.Id)
                    item.SetDraft(DraftFor(current.Id));
            }
        }
    }
}
