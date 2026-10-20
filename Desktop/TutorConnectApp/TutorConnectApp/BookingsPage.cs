using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // FOGLALÁSOK (diák és oktató): állapot szerinti fülek, műveletek, értékelés;
    // oktatónak: saját szabad időpontok kezelése
    // ================================================================
    internal class BookingsPage : PageBase
    {
        /// <summary>A főoldali gomb ezzel nyit meg egy adott fület (pl. "slots").</summary>
        public static string? RequestedTab;

        private readonly UserInfo user;
        private readonly (string Key, string Label)[] tabs;
        private readonly Dictionary<string, SelectableChip> tabChips = new();
        private BookingSummaryDto summary = new();
        private FlowLayoutPanel tabRow = null!;
        private readonly Dictionary<string, CountCard> countCards = new();
        private string filter = "";
        private int loadId;

        public BookingsPage(UserInfo user) : base(
            user.Role == "TUTOR" ? "Foglalások" : "Foglalásaim",
            user.Role == "TUTOR" ? "Fogadd el a kérelmeket, és add meg, mikor érsz rá." : "Kövesd nyomon az óráidat, és foglalj újat.")
        {
            this.user = user;

            var list = new List<(string, string)>
            {
                ("pending", "Függőben"),
                ("upcoming", "Közelgő"),
                ("past", "Korábbi"),
                ("cancelled", "Lemondott")
            };
            if (user.Role == "TUTOR")
                list.Add(("slots", "Időpontjaim"));
            tabs = list.ToArray();
        }

        protected override async Task BuildAsync()
        {
            summary = await ApiClient.GetAsync<BookingSummaryDto>("/api/bookings/summary");
            if (IsDisposed) return;

            if (filter.Length == 0)
            {
                if (RequestedTab != null && tabs.Any(t => t.Key == RequestedTab))
                    filter = RequestedTab;
                else
                    filter = user.Role == "TUTOR" && summary.Pending > 0 ? "pending" : "upcoming";
            }
            RequestedTab = null;

            ClearBody();
            tabChips.Clear();
            countCards.Clear();

            if (user.Role == "STUDENT")
            {
                var top = Ui.Row();
                top.Margin = Ui.Pad(0, 0, 0, 12);
                top.Controls.Add(Ui.PrimaryBtn("+  Új foglalás", (_, _) => Navigate("tutors")));
                AddRow(top);
            }

            // áttekintő: számlálókártyák, amelyekre kattintva az adott lista nyílik meg
            var overview = Ui.WrapRow();
            foreach (var (key, title, glyph, color) in new[]
            {
                ("pending", "Függőben", "\uE121", Theme.Warning),
                ("upcoming", "Közelgő", "\uE787", Theme.Success),
                ("past", "Korábbi", "\uE81C", Theme.MainBlue),
                ("cancelled", "Lemondott", "\uE711", Theme.Muted)
            })
            {
                string cardKey = key;
                var card = new CountCard(title, glyph, color, async () =>
                {
                    if (filter == cardKey) return;
                    filter = cardKey;
                    await LoadListAsync();
                });
                countCards[key] = card;
                overview.Controls.Add(card);
            }
            AddRow(overview);

            tabRow = Ui.WrapRow();
            foreach (var (key, _) in tabs)
            {
                string tabKey = key;
                var chip = new SelectableChip(key);
                chip.Click += async (_, _) =>
                {
                    if (filter == tabKey) return;
                    filter = tabKey;
                    await LoadListAsync();
                };
                tabChips[key] = chip;
                tabRow.Controls.Add(chip);
            }
            AddRow(tabRow);

            await LoadListAsync();
        }

        private int? CountFor(string key) => key switch
        {
            "pending" => summary.Pending,
            "upcoming" => summary.Upcoming,
            "past" => summary.Past,
            "cancelled" => summary.Cancelled,
            _ => null
        };

        private void UpdateTabs()
        {
            foreach (var (key, label) in tabs)
            {
                var chip = tabChips[key];
                int? count = CountFor(key);
                chip.SetText(count.HasValue ? $"{label} ({count})" : label);
                chip.SetSelected(key == filter);

                if (countCards.TryGetValue(key, out var card))
                {
                    card.SetCount(count ?? 0);
                    card.SetSelected(key == filter);
                }
            }
        }

        private async Task LoadListAsync()
        {
            int myLoad = ++loadId;
            UpdateTabs();
            RemoveRowsAfter(tabRow);
            AddRow(Ui.Loading(), animate: false);

            try
            {
                if (filter == "slots")
                {
                    var slots = await ApiClient.GetAsync<List<SlotItemDto>>("/api/availability");
                    if (IsDisposed || myLoad != loadId) return;
                    ShowSlots(slots);
                }
                else
                {
                    var bookings = await ApiClient.GetAsync<List<BookingDto>>($"/api/bookings?status={filter}");
                    if (IsDisposed || myLoad != loadId) return;
                    ShowBookings(bookings);
                }
            }
            catch (ApiException ex)
            {
                if (IsDisposed || myLoad != loadId) return;
                RemoveRowsAfter(tabRow);
                AddRow(Ui.MessageCard(ex.Message, ""));
            }
        }

        // ------------------------------------------------------------
        // Foglalások listája
        // ------------------------------------------------------------

        private void ShowBookings(List<BookingDto> bookings)
        {
            Body.SuspendLayout();
            RemoveRowsAfter(tabRow);

            if (bookings.Count == 0)
                AddRow(Ui.MessageCard("Ebben a kategóriában nincs foglalás.", ""));
            else
                foreach (var booking in bookings)
                    AddRow(BuildBookingCard(booking));

            Body.ResumeLayout(true);
        }

        private CardPanel BuildBookingCard(BookingDto b)
        {
            var card = new CardPanel();

            var head = Ui.Row();
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 12, 0)));
            head.Controls.Add(StatusChip(b.Status));
            card.Controls.Add(head);

            card.Controls.Add(Ui.Lbl((b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen"), 10.5F, Theme.Text));

            string when = Ui.FormatRange(b.StartTime, b.EndTime);
            if (b.Price is > 0) when += $"  ·  {Ui.Money(b.Price.Value)}";
            card.Controls.Add(Ui.Lbl(when, 10F, Theme.Muted));

            if (!string.IsNullOrWhiteSpace(b.Note))
                card.Controls.Add(Ui.Lbl("Üzenet: " + b.Note, 10F, Theme.Muted, wrap: true));

            if (b.Review != null)
            {
                int stars = Math.Clamp(b.Review.Rating, 0, 5);
                string text = "Értékelés: " + new string('★', stars) + new string('☆', 5 - stars);
                if (!string.IsNullOrWhiteSpace(b.Review.Comment)) text += "\n" + b.Review.Comment;
                card.Controls.Add(Ui.Lbl(text, 10F, Theme.Muted, wrap: true));
            }

            bool canReview = b.AsStudent && b.Status == "COMPLETED" && b.Review == null;
            if (b.CanConfirm || b.CanReject || b.CanCancel || canReview)
            {
                var actions = Ui.Row();
                if (b.CanConfirm)
                    actions.Controls.Add(Ui.Btn("Elfogadás", Theme.Success, Theme.White, async (_, _) => await ActAsync(b, "confirm", null)));
                if (b.CanReject)
                    actions.Controls.Add(Ui.Btn("Elutasítás", Theme.CardBg, Theme.Danger,
                        async (_, _) => await ActAsync(b, "reject", "Biztosan elutasítod ezt a foglalást?"), outline: true));
                if (b.CanCancel && !b.CanReject)
                    actions.Controls.Add(Ui.Btn("Lemondás", Theme.CardBg, Theme.Danger,
                        async (_, _) => await ActAsync(b, "cancel", "Biztosan lemondod ezt a foglalást?"), outline: true));
                if (canReview)
                    actions.Controls.Add(Ui.PrimaryBtn("Értékelés írása", async (_, _) => await ReviewAsync(b)));
                card.Controls.Add(actions);
            }

            if (b.AsStudent && b.OtherId > 0)
            {
                var more = Ui.Row();
                more.Controls.Add(Ui.Btn("Oktató profilja", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenTutorProfileAsync(b.OtherId), outline: true));
                card.Controls.Add(more);
            }

            return card;
        }

        /// <summary>A foglalás oktatójának profilja (a már letöltött oktatólistából).</summary>
        private async Task OpenTutorProfileAsync(int tutorId)
        {
            try
            {
                var tutors = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;

                var tutor = tutors.FirstOrDefault(t => t.Id == tutorId);
                if (tutor == null)
                {
                    MessageBox.Show(FindForm(), "Az oktató profilja nem érhető el.", "Oktató profilja", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                await TutorActions.ShowProfileAsync(FindForm(), tutor, Navigate);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a profilt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Label StatusChip(string status) => status switch
        {
            "PENDING" => Ui.Chip("Függőben", Theme.Warning),
            "CONFIRMED" => Ui.Chip("Visszaigazolva", Theme.Success),
            "COMPLETED" => Ui.Chip("Lezajlott", Theme.MainBlue),
            "REJECTED" => Ui.Chip("Elutasítva", Theme.Danger),
            "CANCELLED" => Ui.Chip("Lemondva", Theme.Muted),
            _ => Ui.Chip(status, Theme.Muted)
        };

        private async Task ActAsync(BookingDto booking, string action, string? confirmText)
        {
            if (confirmText != null
                && MessageBox.Show(FindForm(), confirmText, "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.PostAsync($"/api/bookings/{booking.Id}/{action}");
                AppEvents.RaiseBadgesStale();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A művelet nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (!IsDisposed) await LoadAsync(); // összesítő és lista frissítése
        }

        private async Task ReviewAsync(BookingDto booking)
        {
            using var dialog = new ReviewDialog(booking);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                await LoadAsync();
        }

        // ------------------------------------------------------------
        // Oktató: saját szabad időpontok
        // ------------------------------------------------------------

        private void ShowSlots(List<SlotItemDto> slots)
        {
            Body.SuspendLayout();
            RemoveRowsAfter(tabRow);

            var top = Ui.Row();
            top.Margin = Ui.Pad(0, 0, 0, 12);
            top.Controls.Add(Ui.PrimaryBtn("+  Új időpont", async (_, _) => await AddSlotAsync()));
            AddRow(top);

            if (slots.Count == 0)
            {
                AddRow(Ui.MessageCard("Még nem adtál meg szabad időpontot. A diákok csak ezek közül foglalhatnak.", ""));
            }
            else
            {
                foreach (var day in slots.OrderBy(s => s.StartTime).GroupBy(s => s.StartTime.ToLocalTime().Date))
                {
                    var card = new CardPanel();
                    card.Controls.Add(Ui.Lbl(Ui.FormatDay(day.First().StartTime), 11.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));

                    foreach (var slot in day)
                    {
                        var row = Ui.Row();
                        row.Controls.Add(Ui.Lbl(Ui.FormatTimeRange(slot.StartTime, slot.EndTime), 10.5F, Theme.Text, margin: Ui.Pad(0, 9, 16, 0)));
                        row.Controls.Add(slot.IsBooked
                            ? Ui.Chip("Lefoglalva", Theme.Warning)
                            : Ui.Chip("Szabad", Theme.Success));
                        if (!slot.IsBooked)
                        {
                            var delete = Ui.Btn("Törlés", Theme.CardBg, Theme.Danger, async (_, _) => await DeleteSlotAsync(slot), outline: true);
                            delete.Margin = Ui.Pad(8, 0, 0, 0);
                            row.Controls.Add(delete);
                        }
                        card.Controls.Add(row);
                    }

                    AddRow(card);
                }
            }

            Body.ResumeLayout(true);
        }

        private async Task AddSlotAsync()
        {
            using var dialog = new AvailabilityDialog();
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), dialog.ResultMessage, "Időpont mentve", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadListAsync();
        }

        private async Task DeleteSlotAsync(SlotItemDto slot)
        {
            if (MessageBox.Show(FindForm(), "Biztosan törlöd ezt az időpontot?", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                await ApiClient.DeleteAsync($"/api/availability/{slot.Id}");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A törlés nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (!IsDisposed) await LoadListAsync();
        }
    }
}
