using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    // E-mail megerősítést jelző sáv
    public partial class TutorMain
    {
        /// <summary>Figyelmeztető sáv az ablak tetején, amíg az e-mail cím nincs megerősítve.</summary>
        private void BuildBanner()
        {
            if (verifyBanner != null)
            {
                contentPanel.Controls.Remove(verifyBanner);
                verifyBanner.Dispose();
            }

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                BackColor = Theme.Warning,
                Padding = Ui.Pad(16, 10, 16, 6),
                Visible = !Me.EmailVerified
            };

            var text = Ui.Lbl("Az e-mail címed még nincs megerősítve, ezért nem tudsz foglalni, illetve időpontot hirdetni.", 10F, Theme.White, bold: true, margin: Ui.Pad(0, 6, 14, 4));
            bar.Controls.Add(text);
            bar.Controls.Add(Ui.Btn("Levél újraküldése", Theme.White, Color.Black, async (_, _) => await VerifyUi.ResendAsync(this)));
            bar.Controls.Add(Ui.Btn("Már megerősítettem", Theme.White, Color.Black, async (_, _) => await CheckVerifiedAsync(true)));

            verifyBanner = bar;
            contentPanel.Controls.Add(bar);
            // a Fill oldalnak a Z-sorrendben legelöl kell lennie, különben a sáv alá csúszik
            currentPage?.BringToFront();
        }

        /// <summary>Újra lekéri a fiók állapotát (pl. miután a böngészőben megnyitotta a linket).</summary>
        private async Task CheckVerifiedAsync(bool manual)
        {
            if (verifyChecking || IsDisposed) return;
            verifyChecking = true;
            try
            {
                await ApiClient.RefreshUserAsync();
                if (manual && !IsDisposed && !Me.EmailVerified)
                    MessageBox.Show(this, "A szerver szerint az e-mail címed még nincs megerősítve. Nyisd meg a levélben kapott linket, majd próbáld újra.",
                        "E-mail megerősítése", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                if (manual) MessageBox.Show(this, ex.Message, "Nem sikerült frissíteni", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                verifyChecking = false;
            }
        }

        /// <summary>Ha a felhasználó visszatér az ablakhoz (a böngészőből), csendben újraellenőrizzük.</summary>
        protected override async void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (ApiClient.CurrentUser is { EmailVerified: false })
                await CheckVerifiedAsync(false);
        }
    }
}
