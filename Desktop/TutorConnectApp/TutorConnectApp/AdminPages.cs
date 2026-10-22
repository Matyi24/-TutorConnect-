namespace TutorConnectApp
{
    // ================================================================
    // ADMIN OLDALAK: Áttekintés, Jelentések, Felhasználók, Értékelések, Foglalások
    // Ugyanazok a tartalmak és szövegek, mint a webes admin felületen (vsc/html/admin.html); az adatokat a /api/admin/...
    // végpontok adják, amelyek a szerveren is ellenőrzik az adminisztrátori jogosultságot.
    // ================================================================

    /// <summary>Áttekintés: számok, foglalások állapot szerint, legutóbbi admin műveletek.</summary>
    internal sealed class AdminOverviewPage : PageBase
    {
        public AdminOverviewPage() : base("Áttekintés", "A platform állapota egy helyen.") { }

        protected override async Task BuildAsync()
        {
            var data = await ApiClient.GetAsync<AdminStatsDto>("/api/admin/stats");
            if (IsDisposed) return;

            ClearBody();

            bool alert = data.Reports.Open > 0;
            AddRow(new HeroBanner("Admin felület",
                alert ? $"{data.Reports.Open} nyitott jelentés vár elbírálásra." : "Minden jelentés el van bírálva. Szép munka!",
                alert ? "Jelentések megnyitása" : null, () => Navigate("admin-reports")));

            // ---------- számok ----------
            var tiles = Ui.WrapRow();
            tiles.Controls.Add(new StatTile(data.Reports.Open, "", "Nyitott jelentés", "", alert ? Theme.Danger : Theme.Success));
            tiles.Controls.Add(new StatTile(data.Users.Total, "", "Felhasználó összesen", "", Theme.MainBlue));
            tiles.Controls.Add(new StatTile(data.Users.Students, "", "Diák", "", Theme.MainBlue));
            tiles.Controls.Add(new StatTile(data.Users.Tutors, "", "Oktató", "", Theme.MainBlueDark));
            tiles.Controls.Add(new StatTile(data.Users.NewWeek, "", "Új az elmúlt 7 napban", "", Theme.Success));
            tiles.Controls.Add(new StatTile(data.Users.Unverified, "", "Megerősítetlen e-mail", "", data.Users.Unverified > 0 ? Theme.Warning : Theme.Muted));
            tiles.Controls.Add(new StatTile(data.Users.Suspended, "", "Felfüggesztett fiók", "", data.Users.Suspended > 0 ? Theme.Danger : Theme.Muted));
            tiles.Controls.Add(new StatTile(data.Messages, "", "Elküldött üzenet", "", Theme.MainBlue, group: true));
            tiles.Controls.Add(new StatTile(data.Reviews.Count, "",
                data.Reviews.Average is double avg ? $"Értékelés (átlag {avg.ToString("0.0", Ui.Hu)})" : "Értékelés", "", Theme.Warning));
            AddRow(tiles);

            // ---------- foglalások állapot szerint ----------
            var bookingCard = new CardPanel();
            bookingCard.Controls.Add(Ui.Lbl("Foglalások állapot szerint", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            var bars = new RankBars(Theme.MainBlue);
            bars.SetRows(new[] { "PENDING", "CONFIRMED", "COMPLETED", "REJECTED", "CANCELLED" }.Select(key =>
            {
                int n = data.Bookings.GetValueOrDefault(key);
                return new RankBars.Row(AdminUi.BookingStatus(key).Text, n, n.ToString());
            }));
            bookingCard.Controls.Add(bars);
            AddRow(bookingCard);

            // ---------- napló ----------
            var logCard = new CardPanel();
            logCard.Controls.Add(Ui.Lbl("Legutóbbi admin műveletek", 12F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            if (data.Log.Count == 0)
            {
                logCard.Controls.Add(Ui.Lbl("Még nem volt admin művelet.", 10F, Theme.Muted));
            }
            else
            {
                foreach (var item in data.Log)
                {
                    logCard.Controls.Add(Ui.Lbl(AdminUi.ActionName(item.Action) + (string.IsNullOrWhiteSpace(item.Details) ? "" : " – " + item.Details),
                        10.5F, Theme.Text, bold: true, wrap: true, margin: Ui.Pad(0, 4, 0, 0)));
                    logCard.Controls.Add(Ui.Lbl($"{item.AdminName}  ·  {AdminUi.Date(item.CreatedAt)}", 9.5F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6)));
                }
            }
            AddRow(logCard);
        }
    }

    /// <summary>Jelentések: szűrés állapot szerint, kártyák, a részletek ablakban.</summary>
    internal sealed class AdminReportsPage : PageBase
    {
        private string status = "OPEN";
        private int page = 1;
        private int loadId;
        private FlowLayoutPanel chips = null!;

        public AdminReportsPage() : base("Jelentések", "A felhasználók jelentései: nézd át, és hozz döntést.") { }

        public override bool InvalidatesOthers => true;

        protected override async Task BuildAsync()
        {
            ClearBody();

            var store = new Dictionary<string, SelectableChip>();
            chips = AdminUi.ChipRow(new[] { ("OPEN", "Nyitott"), ("RESOLVED", "Intézkedés történt"), ("DISMISSED", "Elutasított"), ("ALL", "Mind") },
                status, async key =>
                {
                    if (status == key) return;
                    status = key;
                    page = 1;
                    foreach (var pair in store) pair.Value.SetSelected(pair.Key == status);
                    await LoadListAsync();
                }, store);
            AddRow(chips);

            await LoadListAsync();
        }

        private async Task LoadListAsync()
        {
            int mine = ++loadId;
            try
            {
                var data = await ApiClient.GetAsync<AdminPage<AdminReport>>($"/api/admin/reports?status={status}&page={page}");
                if (IsDisposed || mine != loadId) return;

                ReplaceRowsAfter(chips, () =>
                {
                    if (data.Items.Count == 0)
                    {
                        AddRow(Ui.MessageCard(status == "OPEN" ? "Nincs nyitott jelentés. Szép munka!" : "Nincs találat.", ""));
                        return;
                    }

                    foreach (var report in data.Items) AddRow(BuildCard(report));
                    AddRow(AdminUi.Pager(data.Page, data.Total, data.PageSize, async p =>
                    {
                        page = p;
                        await LoadListAsync();
                    }));
                });
            }
            catch (ApiException ex)
            {
                if (IsDisposed || mine != loadId) return;
                ReplaceRowsAfter(chips, () => AddRow(Ui.MessageCard(ex.Message, "")));
            }
        }

        private CardPanel BuildCard(AdminReport r)
        {
            var (statusText, statusTone) = AdminUi.ReportStatus(r.Status);
            var card = new AccentCard(AdminUi.Tone(r.Status == "OPEN" ? "bad" : statusTone));

            var head = Ui.Row();
            head.Margin = Ui.Pad(0, 0, 0, 6);
            head.Controls.Add(Ui.Lbl(r.ReportedName ?? "ismeretlen", 13.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 8, 0)));
            head.Controls.Add(Ui.Lbl("(" + AdminUi.RoleName(r.ReportedRole) + ")", 10F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 0)));
            card.Controls.Add(head);

            var badges = Ui.WrapRow();
            badges.Controls.Add(AdminUi.Badge(AdminUi.Reason(r.Reason), "warn"));
            badges.Controls.Add(AdminUi.Badge(statusText, statusTone));
            if (r.ReportedSuspended != null) badges.Controls.Add(AdminUi.Badge("Felfüggesztve", "bad"));
            if (r.OtherReports > 0) badges.Controls.Add(AdminUi.Badge($"{r.OtherReports} további jelentés", "gray"));
            card.Controls.Add(badges);

            card.Controls.Add(Ui.Lbl($"Jelentette: {r.ReporterName}  ·  {AdminUi.Date(r.CreatedAt)}" + (r.MessageId != null ? "  ·  konkrét üzenet" : ""),
                10F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 6)));

            if (!string.IsNullOrWhiteSpace(r.Details))
                card.Controls.Add(new NoticeBox("", "A jelentő megjegyzése", r.Details!, NoticeBox.Tone.Muted));

            var actions = Ui.Row();
            actions.Margin = Ui.Pad(0, 4, 0, 4);
            var open = r.Status == "OPEN"
                ? Ui.PrimaryBtn("Megnyitás", async (_, _) => await OpenAsync(r))
                : Ui.Btn("Részletek", Theme.CardBg, Theme.Text, async (_, _) => await OpenAsync(r), outline: true);
            actions.Controls.Add(open);
            card.Controls.Add(actions);
            return card;
        }

        private async Task OpenAsync(AdminReport report)
        {
            try
            {
                var detail = await ApiClient.GetAsync<AdminReportDetail>("/api/admin/reports/" + report.Id);
                if (IsDisposed) return;

                using var dialog = new ReportDetailDialog(detail);
                dialog.ShowDialog(FindForm());
                if (!dialog.Changed || IsDisposed) return;

                AppEvents.RaiseBadgesStale(); // a menü jelvénye (nyitott jelentések) frissül
                await LoadListAsync();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült megnyitni a jelentést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>Felhasználók: keresés, szűrés, felfüggesztés / feloldás, e-mail kézi megerősítése.</summary>
    internal sealed class AdminUsersPage : PageBase
    {
        private static readonly string[] RoleValues = { "", "STUDENT", "TUTOR", "ADMIN" };
        private static readonly string[] StatusValues = { "", "active", "suspended", "unverified", "deleted" };

        private string query = "";
        private int roleIndex;
        private int statusIndex;
        private int page = 1;
        private int loadId;
        private FlowLayoutPanel filters = null!;
        private SearchBox search = null!;
        private readonly Debouncer searchDebounce;

        public AdminUsersPage() : base("Felhasználók", "Keresés, szűrés és fiókok kezelése.")
        {
            searchDebounce = new Debouncer(300, async () =>
            {
                query = search.Value.Trim();
                page = 1;
                await LoadListAsync();
            });
        }

        public override bool InvalidatesOthers => true;

        protected override void Dispose(bool disposing)
        {
            if (disposing) searchDebounce.Dispose();
            base.Dispose(disposing);
        }

        public override bool FocusSearch()
        {
            if (search == null) return false;
            search.FocusInput();
            return true;
        }

        protected override async Task BuildAsync()
        {
            ClearBody();

            search = new SearchBox("Keresés névre vagy e-mailre...");
            search.SetValue(query);
            search.ValueChanged += (_, _) => searchDebounce.Trigger();
            AddRow(search);

            var role = new DropdownSelect("Szerepkör", new[] { "Minden szerepkör", "Diák", "Oktató", "Admin" }, roleIndex);
            role.SelectionChanged += async (_, _) =>
            {
                roleIndex = role.SelectedIndex;
                page = 1;
                await LoadListAsync();
            };
            var state = new DropdownSelect("Állapot", new[] { "Minden állapot", "Aktív", "Felfüggesztett", "Megerősítetlen e-mail", "Törölt" }, statusIndex);
            state.SelectionChanged += async (_, _) =>
            {
                statusIndex = state.SelectedIndex;
                page = 1;
                await LoadListAsync();
            };

            filters = Ui.WrapRow();
            filters.Controls.Add(role);
            filters.Controls.Add(state);
            AddRow(filters);

            await LoadListAsync();
        }

        private async Task LoadListAsync()
        {
            int mine = ++loadId;
            try
            {
                string url = $"/api/admin/users?q={Uri.EscapeDataString(query)}&role={RoleValues[roleIndex]}&status={StatusValues[statusIndex]}&page={page}";
                var data = await ApiClient.GetAsync<AdminPage<AdminUser>>(url);
                if (IsDisposed || mine != loadId) return;

                ReplaceRowsAfter(filters, () =>
                {
                    if (data.Items.Count == 0)
                    {
                        AddRow(Ui.MessageCard("Nincs találat.", ""));
                        return;
                    }

                    foreach (var user in data.Items) AddRow(BuildCard(user));
                    AddRow(AdminUi.Pager(data.Page, data.Total, data.PageSize, async p =>
                    {
                        page = p;
                        await LoadListAsync();
                    }));
                });
            }
            catch (ApiException ex)
            {
                if (IsDisposed || mine != loadId) return;
                ReplaceRowsAfter(filters, () => AddRow(Ui.MessageCard(ex.Message, "")));
            }
        }

        private CardPanel BuildCard(AdminUser u)
        {
            bool deleted = u.DeletedAt != null;
            bool suspended = u.SuspendedAt != null;
            var card = new AccentCard(deleted ? Theme.Muted : suspended ? Theme.Danger : Theme.Success);

            // ---------- fej: avatar, név, e-mail ----------
            var head = Ui.Row();
            head.Controls.Add(new AvatarCircle(u.FullName, 52));
            var info = Ui.Row();
            info.FlowDirection = FlowDirection.TopDown;
            info.Controls.Add(Ui.Lbl(u.FullName, 13.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 2, 0, 2)));
            info.Controls.Add(Ui.Lbl(u.Email, 10F, Theme.Muted, margin: Ui.Pad(0)));
            head.Controls.Add(info);
            card.Controls.Add(head);

            // ---------- jelvények ----------
            var badges = Ui.WrapRow();
            badges.Margin = Ui.Pad(0, 8, 0, 0);
            badges.Controls.Add(AdminUi.Badge(AdminUi.RoleName(u.Role), "info"));
            if (deleted) badges.Controls.Add(AdminUi.Badge("Törölt", "gray"));
            else if (suspended) badges.Controls.Add(AdminUi.Badge("Felfüggesztett", "bad"));
            else badges.Controls.Add(AdminUi.Badge("Aktív", "ok"));
            if (!deleted && u.EmailVerifiedAt == null) badges.Controls.Add(AdminUi.Badge("E-mail nincs megerősítve", "warn"));
            if (u.OpenReports > 0) badges.Controls.Add(AdminUi.Badge($"{u.OpenReports} nyitott jelentés", "bad"));
            else if (u.AllReports > 0) badges.Controls.Add(AdminUi.Badge($"{u.AllReports} jelentés összesen", "gray"));
            card.Controls.Add(badges);

            card.Controls.Add(Ui.Lbl("Regisztrált: " + AdminUi.Date(u.CreatedAt), 10F, Theme.Muted, margin: Ui.Pad(0, 2, 0, 4)));
            if (suspended && !string.IsNullOrWhiteSpace(u.SuspendedReason))
                card.Controls.Add(new NoticeBox("", "A felfüggesztés indoklása", u.SuspendedReason!, NoticeBox.Tone.Muted));

            // ---------- műveletek ----------
            var actions = Ui.Row();
            actions.Margin = Ui.Pad(0, 4, 0, 4);
            if (!deleted && u.Role != "ADMIN")
            {
                actions.Controls.Add(suspended
                    ? Ui.Btn("Feloldás", Theme.CardBg, Theme.Text, async (_, _) => await UnsuspendAsync(u), outline: true)
                    : Ui.Btn("Felfüggesztés", Theme.CardBg, Theme.Danger, async (_, _) => await SuspendAsync(u), outline: true));
            }
            if (!deleted && u.EmailVerifiedAt == null)
                actions.Controls.Add(Ui.Btn("E-mail megerősítése", Theme.CardBg, Theme.MainBlue, async (_, _) => await VerifyAsync(u), outline: true));
            if (actions.Controls.Count > 0) card.Controls.Add(actions);

            return card;
        }

        private async Task SuspendAsync(AdminUser user)
        {
            using var dialog = new ReasonDialog($"{user.FullName} felfüggesztése", "A felfüggesztett felhasználó nem tud belépni.",
                "Indoklás (ezt a felhasználó belépéskor látni fogja)", "pl. Többszöri szabálysértés", "Felfüggesztés", danger: true);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            await RunAsync(() => ApiClient.PostAsync($"/api/admin/users/{user.Id}/suspend", new { reason = dialog.Reason }));
        }

        private async Task UnsuspendAsync(AdminUser user)
        {
            if (MessageBox.Show(FindForm(), $"Biztosan feloldod {user.FullName} felfüggesztését?", "Felfüggesztés feloldása",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            await RunAsync(() => ApiClient.PostAsync($"/api/admin/users/{user.Id}/unsuspend"));
        }

        private Task VerifyAsync(AdminUser user) => RunAsync(() => ApiClient.PostAsync($"/api/admin/users/{user.Id}/verify-email"));

        private async Task RunAsync(Func<Task> action)
        {
            try
            {
                await action();
                if (!IsDisposed) await LoadListAsync();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A művelet nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>Értékelések: áttekintés és a nem megfelelők törlése (indoklással, naplózva).</summary>
    internal sealed class AdminReviewsPage : PageBase
    {
        private int page = 1;
        private int loadId;
        private Control anchor = null!;

        public AdminReviewsPage() : base("Értékelések", "Az oktatókról írt értékelések; a nem megfelelők törölhetők.") { }

        public override bool InvalidatesOthers => true;

        protected override async Task BuildAsync()
        {
            ClearBody();

            // a lista alatti sorokat cseréljük: egy láthatatlan, üres sor a "horgony"
            anchor = new Panel { Height = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
            AddRow(anchor);

            await LoadListAsync();
        }

        private async Task LoadListAsync()
        {
            int mine = ++loadId;
            try
            {
                var data = await ApiClient.GetAsync<AdminPage<AdminReview>>("/api/admin/reviews?page=" + page);
                if (IsDisposed || mine != loadId) return;

                ReplaceRowsAfter(anchor, () =>
                {
                    if (data.Items.Count == 0)
                    {
                        AddRow(Ui.MessageCard("Még nincs értékelés.", ""));
                        return;
                    }

                    foreach (var review in data.Items) AddRow(BuildCard(review));
                    AddRow(AdminUi.Pager(data.Page, data.Total, data.PageSize, async p =>
                    {
                        page = p;
                        await LoadListAsync();
                    }));
                });
            }
            catch (ApiException ex)
            {
                if (IsDisposed || mine != loadId) return;
                ReplaceRowsAfter(anchor, () => AddRow(Ui.MessageCard(ex.Message, "")));
            }
        }

        private CardPanel BuildCard(AdminReview r)
        {
            var card = new AccentCard(Theme.Star);

            var head = Ui.Row();
            head.Margin = Ui.Pad(0, 0, 0, 4);
            head.Controls.Add(Ui.Lbl(r.TutorName ?? "ismeretlen oktató", 13F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 12, 0)));
            head.Controls.Add(Ui.Lbl(new string('★', Math.Clamp(r.Rating, 0, 5)) + new string('☆', 5 - Math.Clamp(r.Rating, 0, 5)),
                12F, Theme.Star, margin: Ui.Pad(0, 0, 8, 0)));
            head.Controls.Add(Ui.Lbl($"{r.Rating} / 5", 10F, Theme.Muted, margin: Ui.Pad(0, 4, 0, 0)));
            card.Controls.Add(head);

            card.Controls.Add(Ui.Lbl($"Értékelte: {r.StudentName}  ·  {AdminUi.Date(r.CreatedAt)}", 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6)));
            card.Controls.Add(string.IsNullOrWhiteSpace(r.Comment)
                ? Ui.Lbl("Nincs szöveges értékelés.", 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 6))
                : new NoticeBox("", "Vélemény", r.Comment!.Trim(), NoticeBox.Tone.Muted));

            var actions = Ui.Row();
            actions.Margin = Ui.Pad(0, 4, 0, 4);
            actions.Controls.Add(Ui.Btn("Törlés", Theme.CardBg, Theme.Danger, async (_, _) => await DeleteAsync(r), outline: true));
            card.Controls.Add(actions);
            return card;
        }

        private async Task DeleteAsync(AdminReview review)
        {
            using var dialog = new ReasonDialog("Értékelés törlése", $"{review.TutorName} · {review.Rating} / 5",
                "Miért törlöd? (naplózzuk)", "pl. Sértő megfogalmazás", "Törlés", danger: true);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            try
            {
                await ApiClient.DeleteAsync("/api/admin/reviews/" + review.Id, new { reason = dialog.Reason });
                if (!IsDisposed) await LoadListAsync();
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "A törlés nem sikerült", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>Foglalások (csak olvasható): állapot szerinti szűrés és áttekintő sorok.</summary>
    internal sealed class AdminBookingsPage : PageBase
    {
        private string status = "";
        private int page = 1;
        private int loadId;
        private FlowLayoutPanel chips = null!;

        public AdminBookingsPage() : base("Foglalások", "Az összes foglalás áttekintése (csak olvasható).") { }

        protected override async Task BuildAsync()
        {
            ClearBody();

            var store = new Dictionary<string, SelectableChip>();
            chips = AdminUi.ChipRow(new[]
            {
                ("", "Mind"), ("PENDING", "Függőben"), ("CONFIRMED", "Elfogadva"), ("REJECTED", "Elutasítva"), ("CANCELLED", "Lemondva"), ("COMPLETED", "Teljesítve")
            }, status, async key =>
            {
                if (status == key) return;
                status = key;
                page = 1;
                foreach (var pair in store) pair.Value.SetSelected(pair.Key == status);
                await LoadListAsync();
            }, store);
            AddRow(chips);

            await LoadListAsync();
        }

        private async Task LoadListAsync()
        {
            int mine = ++loadId;
            try
            {
                var data = await ApiClient.GetAsync<AdminPage<AdminBooking>>($"/api/admin/bookings?status={status}&page={page}");
                if (IsDisposed || mine != loadId) return;

                ReplaceRowsAfter(chips, () =>
                {
                    if (data.Items.Count == 0)
                    {
                        AddRow(Ui.MessageCard("Nincs találat.", ""));
                        return;
                    }

                    foreach (var booking in data.Items) AddRow(BuildCard(booking));
                    AddRow(AdminUi.Pager(data.Page, data.Total, data.PageSize, async p =>
                    {
                        page = p;
                        await LoadListAsync();
                    }));
                });
            }
            catch (ApiException ex)
            {
                if (IsDisposed || mine != loadId) return;
                ReplaceRowsAfter(chips, () => AddRow(Ui.MessageCard(ex.Message, "")));
            }
        }

        private static CardPanel BuildCard(AdminBooking b)
        {
            var (text, tone) = AdminUi.BookingStatus(b.Status);
            var card = new AccentCard(AdminUi.Tone(tone));

            var head = Ui.Row();
            head.Margin = Ui.Pad(0, 0, 0, 4);
            head.Controls.Add(Ui.Lbl($"#{b.Id}", 10F, Theme.Muted, margin: Ui.Pad(0, 5, 10, 0)));
            head.Controls.Add(Ui.Lbl(b.SubjectName ?? "Tantárgy", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 12, 0)));
            head.Controls.Add(AdminUi.Badge(text, tone));
            card.Controls.Add(head);

            var line = Ui.Row();
            line.Controls.Add(Ui.Lbl("Diák: ", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            line.Controls.Add(Ui.Lbl(b.StudentName ?? "–", 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 18, 0)));
            line.Controls.Add(Ui.Lbl("Oktató: ", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            line.Controls.Add(Ui.Lbl(b.TutorName ?? "–", 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0)));
            card.Controls.Add(line);

            string when = AdminUi.Date(b.StartTime) + (b.Price is > 0 ? "  ·  " + Ui.Money(b.Price.Value) : "");
            card.Controls.Add(Ui.Lbl(when, 10F, Theme.Muted, margin: Ui.Pad(0, 2, 0, 2)));
            return card;
        }
    }
}
