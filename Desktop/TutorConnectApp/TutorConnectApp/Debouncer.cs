namespace TutorConnectApp
{
    /// <summary>
    /// Késlelteti egy művelet futtatását, amíg a hívások el nem csendesednek (pl. gépelés közben a szűrés
    /// nem minden betűnél, hanem a gépelés szünetében fut le). Csak a felületi szálról használható.
    /// </summary>
    internal sealed class Debouncer : IDisposable
    {
        private readonly System.Windows.Forms.Timer timer;

        public Debouncer(int milliseconds, Action action)
        {
            timer = new System.Windows.Forms.Timer { Interval = milliseconds };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                action();
            };
        }

        /// <summary>Újraindítja a visszaszámlálást; a művelet a késleltetés letelte után fut le egyszer.</summary>
        public void Trigger()
        {
            timer.Stop();
            timer.Start();
        }

        /// <summary>Megszakítja a függőben lévő futtatást.</summary>
        public void Stop() => timer.Stop();

        public void Dispose() => timer.Dispose();
    }
}
