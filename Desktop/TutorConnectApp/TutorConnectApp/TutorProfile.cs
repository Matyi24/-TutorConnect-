namespace TutorConnectApp
{
    /// <summary>Mit választott a felhasználó az oktatói profil ablakban.</summary>
    internal enum ProfileChoice { None, Book, Calendar, Message }

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
            using (var dialog = new TutorProfileDialog(tutor))
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                choice = dialog.Choice;
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
        private readonly Label availabilityLabel;
        private readonly FlowLayoutPanel reviewsHost;

        public ProfileChoice Choice { get; private set; }

        public TutorProfileDialog(TutorDto tutor)
            : base(tutor.FullName, SummaryFor(tutor), "Időpont foglalása", Ui.Sz(560, 720), "")
        {
            this.tutor = tutor;

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
            quick.Controls.Add(Ui.Btn("Üzenet küldése", Theme.CardBg, Theme.Text, (_, _) => Finish(ProfileChoice.Message), outline: true));
            quick.Controls.Add(Ui.Btn("Naptár megnyitása", Theme.CardBg, Theme.Text, (_, _) => Finish(ProfileChoice.Calendar), outline: true));

            // a diák elmentheti kedvencnek; ha az oktató új időpontot hirdet, az app értesít (amíg fut)
            if (ApiClient.CurrentUser?.Role == "STUDENT")
            {
                HoverButton? favorite = null;
                favorite = Ui.Btn(FavoriteText(tutor.Id), Theme.CardBg, Theme.Text, (_, _) =>
                {
                    Favorites.Toggle(tutor.Id);
                    favorite!.Text = FavoriteText(tutor.Id);
                }, outline: true);
                quick.Controls.Add(favorite);
            }
            AddBody(quick);

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
            Finish(ProfileChoice.Book);
            return Task.CompletedTask;
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await Task.WhenAll(LoadAvailabilityAsync(), LoadReviewsAsync());
        }

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
