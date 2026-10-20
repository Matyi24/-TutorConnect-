using System.Globalization;

namespace TutorConnectApp
{
    /// <summary>Lezajlott óra értékelése (1–5 csillag + vélemény).</summary>
    internal sealed class ReviewDialog : DialogBase
    {
        private readonly BookingDto booking;
        private readonly StarRating stars;
        private readonly TextBox commentBox;

        public ReviewDialog(BookingDto booking)
            : base("Óra értékelése", $"{booking.SubjectName ?? "Óra"}  ·  {booking.OtherName ?? "oktató"}", "Értékelés mentése", Ui.Sz(560, 500), "\uE734")
        {
            this.booking = booking;

            Section("Hány csillagot adsz?");
            stars = new StarRating();
            AddBody(stars);

            Section("Vélemény (nem kötelező)");
            commentBox = NewTextBox(130, 1000);
            AddBody(commentBox);

            SetFooter("Az értékelés az oktató profilján is megjelenik.");
        }

        protected override async Task OnActionAsync()
        {
            if (stars.Value < 1)
            {
                SetFooter("Válassz 1 és 5 csillag között.", error: true);
                return;
            }

            ActionButton.Enabled = false;
            try
            {
                await ApiClient.PostAsync($"/api/bookings/{booking.Id}/review", new Dictionary<string, object?>
                {
                    ["rating"] = stars.Value,
                    ["comment"] = commentBox.Text.Trim()
                });

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (IsDisposed) return;
                SetFooter(ex.Message, error: true);
                ActionButton.Enabled = true;
            }
        }
    }
}
