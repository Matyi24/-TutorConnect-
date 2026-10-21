namespace TutorConnectApp
{
    // Valós idejű események, olvasottság-jelzés, piszkozat és állapotsor
    internal sealed partial class ChatPage
    {
        private const int ReceiptBaseMs = 8000;
        private const int ReceiptMaxMs = 60_000;

        private int receiptInterval = ReceiptBaseMs;
        private bool receiptsBusy;

        // A beszélgetések listáját és az olvasatlan számlálót a ChatState frissíti (a főablak hívja);
        // itt csak a megnyitott beszélgetés üzenetei változnak.
        private async void OnRealtime(RealtimeEvent e)
        {
            if (IsDisposed) return;

            try
            {
                switch (e.Type)
                {
                    case "new_message" when e.Message != null:
                        await OnNewMessageAsync(e.Message);
                        break;

                    case "message_retracted":
                        if (current?.Id == e.ConversationId) ApplyRetracted(e.MessageId);
                        break;

                    case "message_edited":
                        // az esemény nem hozza az új szöveget, ezért a megnyitott beszélgetést újratöltjük
                        if (selfEdits.Remove(e.MessageId, out long editedAt) && Environment.TickCount64 - editedAt < 10_000)
                            break; // a saját szerkesztésünk visszhangja: már helyben alkalmaztuk

                        if (current?.Id == e.ConversationId)
                        {
                            bool stick = messageScroll.IsNearEnd();
                            await ReloadMessagesAsync(stick);
                            if (!IsDisposed && messages.Count > 0)
                                RefreshPreviewIfLast(messages[^1]);
                        }
                        break;

                    case "typing":
                        if (current?.Id == e.ConversationId) ShowStatus("ír...", error: false);
                        break;
                }
            }
            catch (ApiException)
            {
                // a következő esemény vagy frissítés kijavítja
            }
        }

        private async Task OnNewMessageAsync(MessageDto message)
        {
            if (current?.Id != message.ConversationId) return; // a gyorsítótárba a ChatState teszi be

            AppendMessage(message);

            // csak akkor olvasott, ha a beszélgetés tényleg látszik (keskeny ablaknál a lista mögött nem)
            if (ThreadVisible)
                await ApiClient.PostAsync($"/api/conversations/{message.ConversationId}/read");
        }

        // ------------------------------------------------------------
        // Olvasottság (✓ → ✓✓)
        // ------------------------------------------------------------

        /// <summary>
        /// A szerver nem jelzi az olvasást, ezért amíg van olvasatlan saját üzenet, időnként rákérdezünk. A kérések
        /// ritkulnak, ha nincs változás (a teljes beszélgetést kell letölteni), és sűrűsödnek, ha új üzenetet küldtünk.
        /// </summary>
        private async Task RefreshReceiptsAsync()
        {
            var open = current;
            if (open == null || !ThreadVisible || receiptsBusy || IsDisposed || Form.ActiveForm == null) return;
            if (!messages.Any(m => m.SenderId == me.Id && !m.IsRead && !m.IsRetracted && m.Id > 0)) return;

            receiptsBusy = true;
            try
            {
                bool changed = await FetchMessagesAsync(open, firstLoad: false, scrollToEnd: false, quiet: true);
                SetReceiptInterval(changed ? ReceiptBaseMs : Math.Min(receiptInterval * 3 / 2, ReceiptMaxMs));
            }
            finally
            {
                receiptsBusy = false;
            }
        }

        private void ResetReceiptPolling() => SetReceiptInterval(ReceiptBaseMs);

        private void SetReceiptInterval(int milliseconds)
        {
            receiptInterval = milliseconds;
            if (receiptTimer.Interval != milliseconds) receiptTimer.Interval = milliseconds;
        }

        // ------------------------------------------------------------
        // Piszkozat, gépelés, állapotsor
        // ------------------------------------------------------------

        private void SaveDraft()
        {
            if (current == null) return;

            string text = composer.Value;
            if (text.Length == 0) Drafts.Remove(current.Id);
            else Drafts[current.Id] = text;
            RefreshDraftBadge();
        }

        private void NotifyTyping()
        {
            if (current == null) return;

            long now = Environment.TickCount64;
            if (now - lastTypingSent < 2000) return; // legfeljebb 2 másodpercenként jelzünk
            lastTypingSent = now;
            Realtime.SendTyping(current.Id);
        }

        private void ShowStatus(string text, bool error, bool autoHide = true)
        {
            headerStatus.ForeColor = error ? Theme.Danger : Theme.Muted;
            headerStatus.Text = text;
            statusTimer.Stop();
            if (autoHide && text.Length > 0) statusTimer.Start();
        }
    }
}
