using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    /// <summary>Egy vezérlő újrarajzolásának ideiglenes leállítása és egyben, gyors újrafestése.</summary>
    internal static class Redraw
    {
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint RDW_UPDATENOW = 0x0100;

        [DllImport("user32.dll")]
        private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(nint hWnd, nint lprcUpdate, nint hrgnUpdate, uint flags);

        public static void Suspend(Control control)
        {
            if (control.IsHandleCreated) SendMessage(control.Handle, WM_SETREDRAW, 0, 0);
        }

        /// <summary>Újra engedélyezi a rajzolást, és a vezérlő teljes fáját egyetlen menetben kifesti.</summary>
        public static void Resume(Control control)
        {
            if (!control.IsHandleCreated) return;

            SendMessage(control.Handle, WM_SETREDRAW, 1, 0);

            // a szülőt is újrafestjük, törléssel: ha a tartalom összement (rövidebb lista), a felszabadult
            // területen különben a régi kép maradna, míg valami újrafesti
            PaintNow(control.Parent ?? control, erase: true);
        }

        /// <summary>Az összes gyerekvezérlő azonnali, egymás utáni kifestése: nem jelenik meg darabokban, lassan.</summary>
        public static void PaintNow(Control control, bool erase = false)
        {
            if (control.IsHandleCreated && control.Visible)
                RedrawWindow(control.Handle, 0, 0, RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW | (erase ? RDW_ERASE : 0));
        }
    }
}
