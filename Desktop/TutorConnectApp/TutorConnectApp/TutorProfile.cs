namespace TutorConnectApp
{
    /// <summary>Mit választott a felhasználó az oktatói profil ablakban.</summary>
    internal enum ProfileChoice { None, Book, Calendar, Message, Similar }

    /// <summary>
    /// Az oktatóval kapcsolatos közös műveletek (profil, foglalás, üzenet), amelyeket több oldal is használ:
    /// Oktatók, Chat, Foglalások.
    /// </summary>
    internal static class TutorActions
    {
        /// <summary>Megnyitja az oktató profilját, és végrehajtja, amit a felhasználó ott választott.</summary>
        public static async Task ShowProfileAsync(IWin32Window? owner, TutorDto tutor, Action<string> navigate)
        {
            ProfileChoice choice;
            while (true)
            {
                using var dialog = new TutorProfileDialog(tutor);
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                choice = dialog.Choice;

                // egy hasonló oktatóra kattintva annak a profilja nyílik meg
                if (choice == ProfileChoice.Similar && dialog.SimilarTutor != null)
                {
                    tutor = dialog.SimilarTutor;
                    continue;
                }
                break;
            }

            switch (choice)
            {
                case ProfileChoice.Book:
                    Book(owner, tutor, navigate);
                    break;
                case ProfileChoice.Calendar:
                    CalendarPage.InitialTutorId = tutor.Id;
                    navigate("calendar");
                    break;
                case ProfileChoice.Message:
                    await MessageAsync(owner, tutor, navigate);
                    break;
            }
        }

        public static void Book(IWin32Window? owner, TutorDto tutor, Action<string> navigate)
        {
            using var dialog = new BookDialog(tutor);
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;

            MessageBox.Show(owner,
                "A foglalási kérelmet elküldtük. Az oktató visszaigazolására vár.\nA Foglalás menüpontban követheted.",
                "Foglalás elküldve", MessageBoxButtons.OK, MessageBoxIcon.Information);
            navigate("bookings");
        }

        /// <summary>Beszélgetést indít az oktatóval (vagy megnyitja a meglévőt), és átvált a Chatre.</summary>
        public static async Task MessageAsync(IWin32Window? owner, TutorDto tutor, Action<string> navigate)
        {
            try
            {
                var conversation = await ApiClient.PostAsync<ConversationDto>("/api/conversations",
                    new Dictionary<string, object?> { ["tutor_id"] = tutor.Id });
                ChatPage.PendingConversationId = conversation.Id;
                navigate("chat");
            }
            catch (ApiException ex)
            {
                MessageBox.Show(owner, ex.Message, "Nem sikerült megnyitni a beszélgetést", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    /// <summary>
    /// Egy oktató teljes profilja: bemutatkozás, tantárgyak és óradíjak, szabad időpontok és az összes értékelés.
    /// Innen közvetlenül foglalhat időpontot, nyithatja a naptárát vagy üzenhet neki (a választást a Choice adja vissza).
    /// </summary>
    internal sealed class TutorProfileDialog : DialogBase
    {
        private const int ReviewsShownFirst = 6;

        private readonly TutorDto tutor;
        private readonly bool preview;
        private readonly Label availabilityLabel;
        private readonly FlowLayoutPanel reviewsHost;
        private readonly FlowLayoutPanel similarHost;

        public ProfileChoice Choice { get; private set; }

        /// <summary>A "Hasonló oktatók" közül kiválasztott oktató (Choice == Similar esetén).</summary>
        public TutorDto? SimilarTutor { get; private set; }

        /// <param name="preview">Igaz: az oktató saját profiljának előnézete (így látják a diákok); műveletek nélkül.</param>
        public TutorProfileDialog(TutorDto tutor, bool preview = false)
            : base(tutor.FullName, preview ? "Így látják a diákok a profilodat" : SummaryFor(tutor), preview ? "Bezárás" : "Időpont foglalása", Ui.Sz(560, 720), "\uE77B")
        {
            this.tutor = tutor;
            this.preview = preview;

            Section("Bemutatkozás");
            var bio = Ui.Lbl(string.IsNullOrWhiteSpace(tutor.Bio) ? "Az oktató még nem írt bemutatkozást." : tutor.Bio!.Trim(),
                10.5F, string.IsNullOrWhiteSpace(tutor.Bio) ? Theme.Muted : Theme.Text, margin: Ui.Pad(0));
            bio.MaximumSize = new Size(ContentWidth, 0);
            AddBody(bio);

            Section("Tantárgyak és óradíjak");
            var subjects = NewWrap();
            bool any = false;
            foreach (var (name, price) in TutorsPage.ParseSubjects(tutor.SubjectPrices))
            {
                any = true;
                subjects.Controls.Add(Ui.Chip(price > 0 ? $"{name} · {Ui.Money(price)}/óra" : name));
            }
            if (!any)
                subjects.Controls.Add(Ui.Lbl("Még nincs megadott tantárgy.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            AddBody(subjects);

            Section("Szabad időpontok");
            availabilityLabel = Ui.Lbl("Betöltés...", 10.5F, Theme.Muted, margin: Ui.Pad(0));
            availabilityLabel.MaximumSize = new Size(ContentWidth, 0);
            AddBody(availabilityLabel);

            var quick = Ui.Row();
            if (!preview)
            {
                quick.Controls.Add(Ui.Btn("Üzenet küldése", Theme.CardBg, Theme.Text, (_, _) => Finish(ProfileChoice.Message), outline: true));
                quick.Controls.Add(Ui.Btn("Naptár megnyitása", Theme.CardBg, Theme.Text, (_, _) => Finish(ProfileChoice.Calendar), outline: true));
            }

            // a diák elmentheti kedvencnek; ha az oktató új időpontot hirdet, az app értesít (amíg fut)
            if (!preview && ApiClient.CurrentUser?.Role == "STUDENT")
            {
                HoverButton? favorite = null;
                favorite = Ui.Btn(FavoriteText(tutor.Id), Theme.CardBg, Theme.Text, (_, _) =>
                {
                    Favorites.Toggle(tutor.Id);
                    favorite!.Text = FavoriteText(tutor.Id);
                }, outline: true);
                quick.Controls.Add(favorite);
            }
            if (quick.Controls.Count > 0) AddBody(quick);

            Section(tutor.ReviewCount > 0 ? $"Értékelések ({tutor.ReviewCount})" : "Értékelések");
            reviewsHost = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            reviewsHost.Controls.Add(Ui.Loading());
            AddBody(reviewsHost);

            // hasonló oktatók: azonos tantárgyat tanítók (a diáknak segít választani; előnézetben nem kell)
            similarHost = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            if (!preview)
            {
                Section("Hasonló oktatók");
                similarHost.Controls.Add(Ui.Loading());
                AddBody(similarHost);
            }

            if (preview) DismissButton.Visible = false;

            SetFooter(tutor.ReviewCount > 0
                ? $"Átlagos értékelés: ★ {tutor.AverageRating.ToString("0.0", Ui.Hu)} ({tutor.ReviewCount} értékelés)"
                : "Ennek az oktatónak még nincs értékelése.");
        }

        private static string FavoriteText(int tutorId) =>
            Favorites.IsFavorite(tutorId) ? "♥ Kedvenc oktató" : "♡ Kedvencekhez adom";

        private static string SummaryFor(TutorDto tutor)
        {
            var parts = new List<string>();
            parts.Add(tutor.ReviewCount > 0
                ? $"★ {tutor.AverageRating.ToString("0.0", Ui.Hu)}  ({tutor.ReviewCount} értékelés)"
                : "Még nincs értékelés");
            string range = TutorsPage.PriceRange(tutor);
            if (range.Length > 0) parts.Add(range);
            return string.Join("  ·  ", parts);
        }

        private void Finish(ProfileChoice choice)
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override Task OnActionAsync()
        {
            if (preview) Close();
            else Finish(ProfileChoice.Book);
            return Task.CompletedTask;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await Task.WhenAll(LoadAvailabilityAsync(), LoadReviewsAsync(), LoadSimilarAsync());
        }

        private async Task LoadSimilarAsync()
        {
            if (preview) return;

            try
            {
                var all = await ApiClient.GetCachedAsync<List<TutorDto>>("/api/tutors");
                if (IsDisposed) return;

                var mine = SubjectSet(tutor);
                var similar = all
                    .Where(t => t.Id != tutor.Id)
                    .Select(t => (Tutor: t, Shared: SubjectSet(t).Count(mine.Contains)))
                    .Where(x => x.Shared > 0)
                    .OrderByDescending(x => x.Shared)
                    .ThenByDescending(x => x.Tutor.AverageRating)
                    .Take(4)
                    .ToList();

                Ui.Reset(similarHost);
                if (similar.Count == 0)
                {
                    similarHost.Controls.Add(Ui.Lbl("Nincs másik oktató ugyanezekkel a tantárgyakkal.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
                    return;
                }

                foreach (var (other, _) in similar)
                {
                    var item = new TutorPickItem(other, false, ContentWidth);
                    item.Click += (_, _) =>
                    {
                        SimilarTutor = other;
                        Finish(ProfileChoice.Similar);
                    };
                    similarHost.Controls.Add(item);
                }
            }
            catch (ApiException)
            {
                if (IsDisposed) return;
                Ui.Reset(similarHost);
                similarHost.Controls.Add(Ui.Lbl("A hasonló oktatók most nem érhetők el.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
            }
        }

        private static HashSet<string> SubjectSet(TutorDto t) =>
            (t.Subjects ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.ToLowerInvariant())
                .ToHashSet();

        private async Task LoadAvailabilityAsync()
        {
            try
            {
                var data = await ApiClient.GetAsync<AvailabilityDto>($"/api/tutors/{tutor.Id}/availability");
                if (IsDisposed) return;

                if (data.Slots.Count == 0)
                {
                    availabilityLabel.Text = "Jelenleg nincs szabad időpont.";
                    return;
                }

                var first = data.Slots.OrderBy(s => s.StartTime).First();
                availabilityLabel.ForeColor = Theme.Text;
                availabilityLabel.Text = $"{data.Slots.Count} szabad időpont. A legkorábbi: {Ui.FormatRange(first.StartTime, first.EndTime)}";
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) availabilityLabel.Text = ex.Message;
            }
        }

        private async Task LoadReviewsAsync()
        {
            try
            {
                var reviews = await ApiClient.GetAsync<List<ReviewItemDto>>($"/api/tutors/{tutor.Id}/reviews");
                if (IsDisposed) return;

                Ui.Reset(reviewsHost);
                if (reviews.Count == 0)
                {
                    reviewsHost.Controls.Add(Ui.Lbl("Még nem érkezett értékelés.", 10.5F, Theme.Muted, margin: Ui.Pad(0)));
                    return;
                }

                foreach (var review in reviews.Take(ReviewsShownFirst))
                    AddReview(review);

                if (reviews.Count > ReviewsShownFirst)
                {
                    HoverButton? more = null;
                    more = Ui.Btn($"Mind a(z) {reviews.Count} értékelés megjelenítése", Theme.CardBg, Theme.Text, (_, _) =>
                    {
                        // a gomb a saját eltávolítását váltja ki, ezért a kattintás után hajtjuk végre
                        BeginInvoke(new Action(() =>
                        {
                            reviewsHost.SuspendLayout();
                            reviewsHost.Controls.Remove(more);
                            more!.Dispose();
                            foreach (var review in reviews.Skip(ReviewsShownFirst))
                                AddReview(review);
                            reviewsHost.ResumeLayout(true);
                        }));
                    }, outline: true);
                    reviewsHost.Controls.Add(more);
                }
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                Ui.Reset(reviewsHost);
                reviewsHost.Controls.Add(Ui.Lbl(ex.Message, 10.5F, Theme.Danger, margin: Ui.Pad(0)));
            }
        }

        private void AddReview(ReviewItemDto review)
        {
            var row = Ui.Row();
            row.Controls.Add(Ui.Chip($"★ {review.Rating}", Theme.Warning));
            row.Controls.Add(Ui.Lbl(string.IsNullOrWhiteSpace(review.ReviewerName) ? "Diák" : review.ReviewerName!, 10.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 1, 12, 0)));
            row.Controls.Add(Ui.Lbl(review.CreatedAt is DateTime created ? created.ToLocalTime().ToString("yyyy. MM. dd.", Ui.Hu) : "", 9.5F, Theme.Muted, margin: Ui.Pad(0, 3, 0, 0)));
            reviewsHost.Controls.Add(row);

            if (!string.IsNullOrWhiteSpace(review.Comment))
            {
                var comment = Ui.Lbl(review.Comment!, 10F, Theme.Muted, margin: Ui.Pad(0, 0, 0, 14));
                comment.MaximumSize = new Size(ContentWidth, 0);
                reviewsHost.Controls.Add(comment);
            }
        }
    }
}
