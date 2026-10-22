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
            // oktatónál: hányan jelentkeztek ugyanarra az időpontra (egyet fogadhat el, a többi automatikusan elutasítva)
            var competing = bookings
                .Where(b => b.Status == "PENDING" && !b.AsStudent)
                .GroupBy(b => (b.StartTime, b.EndTime))
                .ToDictionary(g => g.Key, g => g.ToList());

            ReplaceRowsAfter(tabRow, () =>
            {
                if (bookings.Count == 0)
                    AddRow(Ui.MessageCard("Ebben a kategóriában nincs foglalás.", "\uE787"));
                else
                    foreach (var booking in bookings)
                        AddRow(BuildBookingCard(booking, RivalsOf(booking, competing)));
            });
        }

        /// <summary>A többi függő jelentkező neve ugyanarra az időpontra (csak oktatónál és függő jelentkezésnél).</summary>
        private static IReadOnlyList<string> RivalsOf(BookingDto booking, Dictionary<(DateTime, DateTime), List<BookingDto>> competing)
        {
            if (booking.Status != "PENDING" || booking.AsStudent) return Array.Empty<string>();
            if (!competing.TryGetValue((booking.StartTime, booking.EndTime), out var group)) return Array.Empty<string>();

            return group.Where(other => other.Id != booking.Id).Select(other => other.OtherName ?? "ismeretlen").ToList();
        }

        /// <summary>A kártya bal szélének színe az állapot szerint.</summary>
        private static Color StatusColor(string status) => status switch
        {
            "PENDING" => Theme.Warning,
            "CONFIRMED" => Theme.Success,
            "COMPLETED" => Theme.MainBlue,
            "REJECTED" => Theme.Danger,
            _ => Theme.Muted
        };

        /// <summary>Egy adatsor ikonnal: pl. a partner neve, az időpont, az ár.</summary>
        private static FlowLayoutPanel InfoLine(string glyph, string text, bool bold = false, Color? color = null)
        {
            var row = Ui.Row();
            row.Margin = Ui.Pad(0, 0, 0, 6);

            var icon = new Label
            {
                Text = glyph,
                Font = Ui.IconFont(11F),
                ForeColor = Theme.MainBlue,
                AutoSize = false,
                Size = new Size(Ui.S(26), Ui.LineHeight(Ui.MakeFont(10.5F)) + Ui.S(2)),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Margin = Ui.Pad(0)
            };
            row.Controls.Add(icon);
            row.Controls.Add(Ui.Lbl(text, 10.5F, color ?? Theme.Text, bold: bold, margin: Ui.Pad(0, 1, 0, 0)));
            return row;
        }

        /// <param name="rivals">A többi diák neve, aki ugyanerre az időpontra jelentkezett (oktatónál, függő jelentkezésnél).</param>
        private CardPanel BuildBookingCard(BookingDto b, IReadOnlyList<string>? rivals = null)
        {
            rivals ??= Array.Empty<string>();
            var card = new AccentCard(StatusColor(b.Status));

            // ---------- fej: tantárgy, állapot ----------
            var head = Ui.Row();
            head.Margin = Ui.Pad(0, 0, 0, 10);
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 13.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 14, 0)));
            head.Controls.Add(StatusChip(b.Status));
            if (rivals.Count > 0)
                head.Controls.Add(Ui.Chip($"{rivals.Count + 1} jelentkező", Theme.MainBlue));
            card.Controls.Add(head);

            // ---------- adatok ----------
            card.Controls.Add(InfoLine("\uE77B", (b.AsStudent ? "Oktató: " : "Diák: ") + (b.OtherName ?? "ismeretlen")));
            card.Controls.Add(InfoLine("\uE787", Ui.FormatRange(b.StartTime, b.EndTime)));
            if (b.Price is > 0)
                card.Controls.Add(InfoLine("\uE8C7", Ui.Money(b.Price.Value), bold: true));

            // ---------- tudnivalók ----------
            if (rivals.Count > 0 && b.CanConfirm)
            {
                string others = string.Join(", ", rivals);
                card.Controls.Add(new NoticeBox("\uE716", $"{rivals.Count + 1} diák jelentkezett erre az időpontra",
                    $"Egyet fogadhatsz el, a többi jelentkezés automatikusan elutasításra kerül. Többi jelentkező: {others}."));
            }

            if (b.Status == "PENDING" && b.AsStudent)
                card.Controls.Add(new NoticeBox("\uE946", "Az oktató választ a jelentkezők közül",
                    "Ha másik diákot fogad el erre az időpontra, ez a jelentkezésed automatikusan elutasításra kerül."));

            if (!string.IsNullOrWhiteSpace(b.Note))
                card.Controls.Add(new NoticeBox("\uE8BD", b.AsStudent ? "Üzeneted" : "A diák üzenete", b.Note!.Trim(), NoticeBox.Tone.Muted));

            if (b.Review != null)
            {
                int stars = Math.Clamp(b.Review.Rating, 0, 5);
                string text = new string('★', stars) + new string('☆', 5 - stars);
                if (!string.IsNullOrWhiteSpace(b.Review.Comment)) text += "\n" + b.Review.Comment;
                card.Controls.Add(new NoticeBox("\uE734", "Értékelés", text, NoticeBox.Tone.Muted));
            }

            if (b.Status is "PENDING" or "CONFIRMED" or "COMPLETED")
                card.Controls.Add(new LessonNoteRow(b));

            // ---------- műveletek ----------
            bool canReview = b.AsStudent && b.Status == "COMPLETED" && b.Review == null;
            if (b.CanConfirm || b.CanReject || b.CanCancel || canReview)
            {
                var actions = Ui.Row();
                actions.Margin = Ui.Pad(0, 6, 0, 4);
                if (b.CanConfirm)
                    actions.Controls.Add(Ui.Btn("Elfogadás", Theme.Success, Theme.White, async (_, _) => await ActAsync(b, "confirm",
                        rivals.Count > 0
                            ? $"Elfogadod ezt a jelentkezést ({b.OtherName})? Erre az időpontra a másik {rivals.Count} jelentkezőt automatikusan elutasítjuk."
                            : null)));
                if (b.CanReject)
                    actions.Controls.Add(Ui.Btn("Elutasítás", Theme.CardBg, Theme.Danger,
                        async (_, _) => await ActAsync(b, "reject", "Biztosan elutasítod ezt a foglalást?"), outline: true));
                if (b.CanCancel && !b.CanReject)
                    actions.Controls.Add(Ui.Btn("Lemondás", Theme.CardBg, Theme.Danger,
                        async (_, _) => await ActAsync(b, "cancel", "Biztosan lemondod ezt a foglalást?"), outline: true));
                if (canReview)
                    actions.Controls.Add(Ui.PrimaryBtn("Értékelés írása", async (_, _) => await ReviewAsync(b)));
                if (b.AsStudent && b.OtherId > 0)
                    actions.Controls.Add(Ui.Btn("Oktató profilja", Theme.CardBg, Theme.MainBlue, async (_, _) => await OpenTutorProfileAsync(b.OtherId), outline: true));
                card.Controls.Add(actions);
            }
            else if (b.AsStudent && b.OtherId > 0)
            {
                var more = Ui.Row();
                more.Margin = Ui.Pad(0, 6, 0, 4);
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
                        if (!slot.IsBooked && slot.Requests > 0)
                            row.Controls.Add(Ui.Chip($"{slot.Requests} jelentkező", Theme.MainBlue));
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
            string question = slot.Requests > 0
                ? $"Erre az időpontra {slot.Requests} diák jelentkezett. Ha törlöd, a jelentkezések automatikusan elutasításra kerülnek.\n\nBiztosan törlöd?"
                : "Biztosan törlöd ezt az időpontot?";
            if (MessageBox.Show(FindForm(), question, "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
