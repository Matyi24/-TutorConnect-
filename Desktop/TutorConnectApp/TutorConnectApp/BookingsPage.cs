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

        public override bool HasPendingRequest => RequestedTab != null;
        public override bool InvalidatesOthers => true;

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

            if (RequestedTab != null && tabs.Any(t => t.Key == RequestedTab))
                filter = RequestedTab;
            else if (filter.Length == 0)
                filter = user.Role == "TUTOR" && summary.Pending > 0 ? "pending" : "upcoming";
            RequestedTab = null;

            ClearBody();
            tabChips.Clear();
            countCards.Clear();

            var top = Ui.Row();
            top.Margin = Ui.Pad(0, 0, 0, 12);
            if (user.Role == "STUDENT")
                top.Controls.Add(Ui.PrimaryBtn("+  Új foglalás", (_, _) => Navigate("tutors")));
            top.Controls.Add(Ui.Btn("Exportálás (CSV)", Theme.CardBg, Theme.Text,
                async (_, _) => await BookingExport.ExportAllAsync(FindForm(), user.Role), outline: true));
            top.Controls.Add(Ui.Btn("Órarend nyomtatása", Theme.CardBg, Theme.Text,
                async (_, _) => await BookingExport.PrintTimetableAsync(FindForm(), user.Name), outline: true));
            AddRow(top);

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
            // a régi lista az új megérkezéséig látszik (nincs üres villanás); az új lista egyben cseréli le

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
                AddRow(Ui.MessageCard(ex.Message, "\uE783"));
            }
        }

        // ------------------------------------------------------------
        // Foglalások listája
        // ------------------------------------------------------------

        private void ShowBookings(List<BookingDto> bookings)
        {
            ReplaceRowsAfter(tabRow, () =>
            {
                if (bookings.Count == 0)
                    AddRow(Ui.MessageCard("Ebben a kategóriában nincs foglalás.", "\uE787"));
                else
                    foreach (var booking in bookings)
                        AddRow(BuildBookingCard(booking));
            });
        }

        /// <summary>A kártya színe az állapot szerint (a bal sáv, a dátum-csempe és az állapotjelző).</summary>
        private static Color StatusColor(string status) => status switch
        {
            "PENDING" => Theme.Warning,
            "CONFIRMED" => Theme.Success,
            "COMPLETED" => Theme.MainBlue,
            "REJECTED" => Theme.Danger,
            _ => Theme.Muted
        };

        private static string StatusName(string status) => status switch
        {
            "PENDING" => "Függőben",
            "CONFIRMED" => "Visszaigazolva",
            "COMPLETED" => "Lezajlott",
            "REJECTED" => "Elutasítva",
            "CANCELLED" => "Lemondva",
            _ => status
        };

        private CardPanel BuildBookingCard(BookingDto b)
        {
            var card = new AccentCard(StatusColor(b.Status));

            // ---------- a foglalás jegye: mikor, kivel, mennyiért, milyen állapotban ----------
            card.Controls.Add(new BookingTicket(b, StatusColor(b.Status), StatusName(b.Status)));

            // ---------- kiegészítők: üzenet, értékelés, jegyzet ----------
            if (b.Status == "PENDING" && !b.AsStudent)
                card.Controls.Add(new NoticeBox("\uE946", "Döntésre vár",
                    "A diák jelentkezett erre az időpontra. Fogadd el, vagy utasítsd el; a diák értesítést kap."));
            else if (b.Status == "PENDING" && b.AsStudent)
                card.Controls.Add(new NoticeBox("\uE946", "Az oktató válaszára vársz",
                    "Amint az oktató dönt, értesítést kapsz. Addig a jelentkezést bármikor lemondhatod."));

            if (!string.IsNullOrWhiteSpace(b.Note))
                card.Controls.Add(new NoticeBox("\uE8BD", b.AsStudent ? "Az üzeneted" : "A diák üzenete", b.Note!.Trim(), NoticeBox.Tone.Muted));

            if (b.Review != null)
            {
                int stars = Math.Clamp(b.Review.Rating, 0, 5);
                string text = new string('★', stars) + new string('☆', 5 - stars);
                if (!string.IsNullOrWhiteSpace(b.Review.Comment)) text += "\n" + b.Review.Comment;
                card.Controls.Add(new NoticeBox("\uE734", "Értékelés", text, NoticeBox.Tone.Muted));
            }

            // a jegyzet (ha van) látszik, a szerkesztése a műveletek között érhető el
            bool canNote = b.Status is "PENDING" or "CONFIRMED" or "COMPLETED";
            string note = canNote ? LessonNotes.Get(b.Id) : "";
            if (note.Length > 0)
                card.Controls.Add(new NoticeBox("\uE70F", "Jegyzetem", note, NoticeBox.Tone.Muted));

            // ---------- műveletek: egy sorban, elválasztó vonal alatt ----------
            bool canReview = b.AsStudent && b.Status == "COMPLETED" && b.Review == null;
            var buttons = new List<PillButton>();
            if (b.CanConfirm)
                buttons.Add(new PillButton("Elfogadás", Theme.Success, PillStyle.Filled, "\uE73E"));
            if (canReview)
                buttons.Add(new PillButton("Értékelés írása", Theme.MainBlue, PillStyle.Filled, "\uE734"));
            if (b.CanReject)
                buttons.Add(new PillButton("Elutasítás", Theme.Danger, PillStyle.Soft, "\uE711"));
            if (b.CanCancel && !b.CanReject)
                buttons.Add(new PillButton("Lemondás", Theme.Danger, PillStyle.Soft, "\uE711"));
            if (b.AsStudent && b.OtherId > 0)
                buttons.Add(new PillButton("Oktató profilja", Theme.MainBlue, PillStyle.Soft, "\uE77B"));
            if (canNote)
                buttons.Add(new PillButton(note.Length > 0 ? "Jegyzet szerkesztése" : "Jegyzet", Theme.Muted, PillStyle.Ghost, "\uE70F"));

            if (buttons.Count > 0)
            {
                card.Controls.Add(new Panel { Height = 1, BackColor = Theme.CardBorder, Tag = "fill", Margin = Ui.Pad(0, 6, 0, 12) });

                var actions = Ui.WrapRow();
                actions.Margin = Ui.Pad(0, 0, 0, 2);
                foreach (var button in buttons)
                {
                    button.Margin = Ui.Pad(0, 0, 10, 8);
                    string label = button.Text;
                    button.Click += async (_, _) =>
                    {
                        switch (label)
                        {
                            case "Elfogadás": await ActAsync(b, "confirm", null); break;
                            case "Elutasítás": await ActAsync(b, "reject", "Biztosan elutasítod ezt a foglalást?"); break;
                            case "Lemondás": await ActAsync(b, "cancel", "Biztosan lemondod ezt a foglalást?"); break;
                            case "Értékelés írása": await ReviewAsync(b); break;
                            case "Oktató profilja": await OpenTutorProfileAsync(b.OtherId); break;
                            default: await EditNoteAsync(b); break;
                        }
                    };
                    actions.Controls.Add(button);
                }
                card.Controls.Add(actions);
            }

            return card;
        }

        /// <summary>Jegyzet szerkesztése; mentés után a lista újrarajzolódik (az új jegyzettel).</summary>
        private async Task EditNoteAsync(BookingDto booking)
        {
            using var dialog = new NoteDialog(booking);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                await LoadListAsync();
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

            if (!IsDisposed) await ReloadAsync(); // összesítő és lista frissítése
        }

        private async Task ReviewAsync(BookingDto booking)
        {
            using var dialog = new ReviewDialog(booking);
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK && !IsDisposed)
                await ReloadAsync();
        }

        // ------------------------------------------------------------
        // Oktató: saját szabad időpontok
        // ------------------------------------------------------------

        private void ShowSlots(List<SlotItemDto> slots)
        {
            ReplaceRowsAfter(tabRow, () =>
            {

            var top = Ui.Row();
            top.Margin = Ui.Pad(0, 0, 0, 12);
            top.Controls.Add(Ui.PrimaryBtn("+  Új időpont", async (_, _) => await AddSlotAsync()));
            AddRow(top);

            if (slots.Count == 0)
            {
                AddRow(Ui.MessageCard("Még nem adtál meg szabad időpontot. A diákok csak ezek közül foglalhatnak.", "\uE787"));
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

            });
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
