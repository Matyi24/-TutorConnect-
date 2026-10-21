namespace TutorConnectApp
{
    /// <summary>Billentyűparancsok és egérműveletek súgója (F1, gyorskereső, Beállítások).</summary>
    internal sealed class ShortcutsDialog : DialogBase
    {
        public ShortcutsDialog(string role)
            : base("Billentyűparancsok", "Gyorsabban dolgozhatsz, ha ismered a parancsokat.", "Rendben", Ui.Sz(560, 640), "\uE765")
        {
            DismissButton.Visible = false;

            Section("Általános");
            AddShortcut("Ctrl + K", "Gyorskereső: oldalak, beszélgetések, oktatók");
            AddShortcut("Ctrl + 1 … 9", "Ugrás a menü adott pontjára");
            AddShortcut("Ctrl + F", "Keresés az oldalon (Chat, Oktatók, Tantárgyak)");
            AddShortcut("F1", "Ez a súgó");
            AddShortcut("Esc", "Felugró ablak bezárása");

            Section("Chat");
            AddShortcut("Enter", "Üzenet küldése");
            AddShortcut("Shift + Enter", "Új sor az üzenetben");
            AddShortcut("Ctrl + V", "Vágólapon lévő kép vagy fájl küldése");
            AddShortcut("Jobb kattintás", "Üzeneten: másolás, hivatkozás megnyitása, szerkesztés, visszavonás");
            AddShortcut("Behúzás", "Fájl behúzása az üzenetek fölé: csatolmány küldése");

            if (role is "TUTOR" or "STUDENT")
            {
                Section("Naptár");
                if (role == "TUTOR")
                {
                    AddShortcut("Húzás", "Üres részen: szabad időpont kijelölése");
                    AddShortcut("Húzás", "Meglévő időponton: áthelyezés");
                    AddShortcut("Jobb kattintás", "Időponton: másolás vagy törlés");
                }
                else
                {
                    AddShortcut("Kattintás", "Szabad (zöld) időponton: jelentkezés az órára");
                }
            }

            SetFooter("");
        }

        private void AddShortcut(string keys, string description)
        {
            var row = Ui.Row();
            row.Margin = Ui.Pad(0, 0, 0, 6);

            var caps = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Size = new Size(Ui.S(190), Ui.S(30)),
                BackColor = Color.Transparent,
                Margin = Ui.Pad(0, 0, 12, 0),
                Padding = Ui.Pad(0)
            };
            foreach (string part in keys.Split(" + "))
            {
                var cap = Ui.Chip(part, Theme.ChipBg, Theme.ChipFg);
                cap.Margin = Ui.Pad(0, 0, 6, 0);
                caps.Controls.Add(cap);
            }

            var text = Ui.Lbl(description, 10F, Theme.Text, wrap: true, margin: Ui.Pad(0, 4, 0, 0));
            text.MaximumSize = new Size(ContentWidth - Ui.S(190) - Ui.S(24), 0);

            row.Controls.Add(caps);
            row.Controls.Add(text);
            Body.Controls.Add(row);
        }

        protected override Task OnActionAsync()
        {
            Close();
            return Task.CompletedTask;
        }
    }
}
