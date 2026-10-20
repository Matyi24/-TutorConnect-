using System.Globalization;

namespace TutorConnectApp
{
    // ================================================================
    // BEÁLLÍTÁSOK: téma, animációk, fiók- és alkalmazásinfó
    // ================================================================
    internal class SettingsPage : PageBase
    {
        private readonly UserInfo user;

        public SettingsPage(UserInfo user) : base("Beállítások", "Szabd személyre az alkalmazást.")
        {
            this.user = user;
        }

        protected override Task BuildAsync()
        {
            ClearBody();

            var look = new CardPanel();
            look.Controls.Add(Ui.Lbl("Megjelenés", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            look.Controls.Add(SwitchRow("Sötét mód", "Kíméletesebb a szemnek, különösen este.", Theme.DarkSetting,
                // az oldal újraépül, ezért a váltást a kattintás kezelése után hajtjuk végre
                on => BeginInvoke(new Action(() => Theme.SetDark(on)))));
            look.Controls.Add(SwitchRow("Animációk", "Finom mozgások és átmenetek a felületen.", Anim.Enabled,
                on => Anim.SetEnabled(on)));
            AddRow(look);

            var notifications = new CardPanel();
            notifications.Controls.Add(Ui.Lbl("Értesítések", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));
            notifications.Controls.Add(SwitchRow("Tálcaértesítések", "Új üzenetről és foglalás-változásról jelez, ha az ablak nincs előtérben.",
                AppSettings.NotificationsEnabled, on => AppSettings.SetNotifications(on)));
            if (user.Role is "STUDENT" or "TUTOR")
                notifications.Controls.Add(SwitchRow("Emlékeztető az órákra", "Az óra előtt 1 órával és 10 perccel értesít (amíg az app fut).",
                    AppSettings.RemindersEnabled, on => AppSettings.SetReminders(on)));
            if (user.Role == "STUDENT")
                notifications.Controls.Add(SwitchRow("Kedvenc oktatók új időpontjai", "Jelez, ha egy kedvenc oktatód új szabad időpontot hirdet (amíg az app fut).",
                    AppSettings.FavoriteAlertsEnabled, on => AppSettings.SetFavoriteAlerts(on)));
            AddRow(notifications);

            AddSystemCard();
            AddPrivacyCard();

            var account = new CardPanel();
            account.Controls.Add(Ui.Lbl("Fiók", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
            account.Controls.Add(Ui.KeyValue("Név", user.Name));
            account.Controls.Add(Ui.KeyValue("E-mail", user.Email));
            account.Controls.Add(Ui.KeyValue("Szerepkör", Ui.RoleName(user.Role)));
            AddRow(account);

            var app = new CardPanel();
            app.Controls.Add(Ui.Lbl("Alkalmazás", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 10)));
            app.Controls.Add(Ui.KeyValue("Szerver", ApiClient.BaseUrl));
            app.Controls.Add(Ui.Lbl("A szerver címét a TUTORCONNECT_URL környezeti változóval lehet módosítani.", 9.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 6, 0, 0)));
            AddRow(app);

            return Task.CompletedTask;
        }

        // ------------------------------------------------------------
        // Adatkezelés: tájékoztatók, adatexport, mentett belépés, fióktörlés
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // Rendszer és kisegítő lehetőségek
        // ------------------------------------------------------------

        private void AddSystemCard()
        {
            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl("Rendszer és kisegítő lehetőségek", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 8)));

            card.Controls.Add(SwitchRow("Magas kontraszt", "Fekete háttér, fehér szöveg és erős keretek a jobb olvashatóságért.", Theme.HighContrast,
                // az oldal újraépül, ezért a váltást a kattintás kezelése után hajtjuk végre
                on => BeginInvoke(new Action(() => Theme.SetHighContrast(on)))));

            card.Controls.Add(SwitchRow("Bezáráskor tálcára kicsinyítés", "Az ablak bezárása után az app a tálcán fut tovább, így az értesítések megérkeznek. Kilépés: a tálcaikon menüjében.",
                AppSettings.MinimizeToTray, on => AppSettings.SetMinimizeToTray(on)));

            card.Controls.Add(SwitchRow("Indítás a Windowsszal", "Bejelentkezés után az app magától elindul a háttérben (a tálcán).",
                AutoStart.IsEnabled, on =>
                {
                    if (AutoStart.SetEnabled(on)) return;

                    MessageBox.Show(FindForm(), "Az automatikus indítás beállítása nem sikerült.", "Indítás a Windowsszal", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    BeginInvoke(new Action(() => _ = LoadAsync())); // a kapcsoló visszaáll a tényleges állapotra
                }));

            card.Controls.Add(Ui.Lbl("Billentyűparancsok:  Ctrl+K gyorskereső  ·  Ctrl+1…9 oldalváltás  ·  Esc ablak bezárása  ·  Tab / Enter: kezelés billentyűzettel",
                9.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 8, 0, 0)));
            AddRow(card);
        }

        private void AddPrivacyCard()
        {
            var card = new CardPanel();
            card.Controls.Add(Ui.Lbl("Adatkezelés", 12.5F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 6)));
            card.Controls.Add(Ui.Lbl("Az adataid a TutorConnect szerverén tárolódnak. Itt elolvashatod a tájékoztatókat, letöltheted az adataidat, vagy törölheted a fiókodat.",
                9.5F, Theme.Muted, wrap: true, margin: Ui.Pad(0, 0, 0, 10)));

            var documents = Ui.Row();
            documents.Controls.Add(Ui.Btn("Adatvédelmi tájékoztató", Theme.CardBg, Theme.Text, (_, _) => Web.Open("/adatvedelem"), outline: true));
            documents.Controls.Add(Ui.Btn("Felhasználási feltételek", Theme.CardBg, Theme.Text, (_, _) => Web.Open("/felhasz_feltetelek"), outline: true));
            card.Controls.Add(documents);

            var data = Ui.Row();
            data.Controls.Add(Ui.Btn("Adataim letöltése (JSON)", Theme.CardBg, Theme.Text, async (_, _) => await ExportDataAsync(), outline: true));
            data.Controls.Add(Ui.Btn("Mentett belépés törlése", Theme.CardBg, Theme.Text, (_, _) =>
            {
                ApiClient.RememberSession(false);
                MessageBox.Show(FindForm(), "A mentett belépést töröltük. Következő induláskor újra be kell jelentkezned.",
                    "Mentett belépés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, outline: true));
            card.Controls.Add(data);

            if (user.Role != "ADMIN")
            {
                card.Controls.Add(Ui.Lbl("Veszélyes zóna", 10.5F, Theme.Danger, bold: true, margin: Ui.Pad(0, 12, 0, 6)));
                card.Controls.Add(Ui.Btn("Fiók törlése", Theme.CardBg, Theme.Danger, (_, _) => DeleteAccount(), outline: true));
            }
            AddRow(card);
        }

        private void DeleteAccount()
        {
            using var dialog = new DeleteAccountDialog();
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || IsDisposed) return;

            MessageBox.Show(FindForm(), "A fiókod törölve lett. Köszönjük, hogy használtad a TutorConnectet!", "Fiók törölve",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RequestLogout(); // a szerver már megszüntette a munkamenetet: vissza a belépőhöz
        }

        /// <summary>A saját fiók- és foglalási adatok mentése egy JSON-fájlba.</summary>
        private async Task ExportDataAsync()
        {
            try
            {
                var sections = new List<(string Name, string Json)> { ("fiok", await ApiClient.GetRawAsync("/api/account")) };
                foreach (string status in new[] { "pending", "upcoming", "past", "cancelled" })
                    sections.Add(($"foglalasok_{status}", await ApiClient.GetRawAsync($"/api/bookings?status={status}")));
                if (IsDisposed) return;

                using var dialog = new SaveFileDialog
                {
                    Title = "Adataim mentése",
                    Filter = "JSON fájl (*.json)|*.json",
                    FileName = "tutorconnect-adataim.json"
                };
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

                using (var stream = File.Create(dialog.FileName))
                using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions
                {
                    Indented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }))
                {
                    writer.WriteStartObject();
                    writer.WriteString("exportalva", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    foreach (var (name, json) in sections)
                    {
                        using var document = System.Text.Json.JsonDocument.Parse(json);
                        writer.WritePropertyName(name);
                        document.RootElement.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }

                MessageBox.Show(FindForm(), "Az adataid elmentve.", "Adatexport", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Nem sikerült letölteni az adatokat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                MessageBox.Show(FindForm(), "A fájl mentése nem sikerült: " + ex.Message, "Nem sikerült letölteni az adatokat",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Egy beállítás-sor: bal oldalt cím és leírás, jobb oldalt a kapcsoló.</summary>
        private static Control SwitchRow(string title, string description, bool initial, Action<bool> onChanged)
        {
            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Height = Ui.S(62),
                BackColor = Color.Transparent,
                Margin = Ui.Pad(0, 0, 0, 4),
                Tag = "fill" // a CardPanel a teljes belső szélességre húzza
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(70)));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var text = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Left,
                Margin = Ui.Pad(0)
            };
            text.Controls.Add(Ui.Lbl(title, 11F, Theme.Text, bold: true, margin: Ui.Pad(0, 0, 0, 2)));
            text.Controls.Add(Ui.Lbl(description, 9.5F, Theme.Muted, margin: Ui.Pad(0)));

            var toggle = new ToggleSwitch(initial) { Anchor = AnchorStyles.Right, Margin = Ui.Pad(0), AccessibleName = title };
            toggle.Toggled += (_, _) => onChanged(toggle.IsOn);

            table.Controls.Add(text, 0, 0);
            table.Controls.Add(toggle, 1, 0);
            return table;
        }
    }
}
