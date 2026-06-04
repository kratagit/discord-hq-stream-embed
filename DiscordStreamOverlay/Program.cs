using System;
using System.Threading;
using System.Windows.Forms;

namespace DiscordStreamOverlay
{
    static class Program
    {
        private static string appGuid = "Discord_Stream_Overlay_SingleInstance_Mutex";

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, "Global\\" + appGuid))
            {
                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show("Instance already running");
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApplicationContext());
            }
        }
    }
}