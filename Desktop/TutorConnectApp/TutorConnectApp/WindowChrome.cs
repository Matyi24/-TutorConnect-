using System.Runtime.InteropServices;

namespace TutorConnectApp
{
    /// <summary>
    /// Az ablak keretének (címsor + szegély) színezése a Windows DWM API-val.
    /// Címsor- és szegélyszín: Windows 11-en működik; Windows 10-en csak a sötét címsor.
    /// </summary>
    internal static class WindowChrome
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void ApplyBlack(Form form)
        {
            int dark = 1;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            // COLORREF formátum: 0x00BBGGRR
            int black = 0x000000;
            int white = 0xFFFFFF;
            DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref black, sizeof(int));
            DwmSetWindowAttribute(form.Handle, DWMWA_BORDER_COLOR, ref black, sizeof(int));
            DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref white, sizeof(int));
        }
    }
}
