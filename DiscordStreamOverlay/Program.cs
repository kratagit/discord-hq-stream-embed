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
                    MessageBox.Show(
                        "Discord Stream Overlay is already running.\n\nPlease check your taskbar for the settings window, or use your configured hotkey to toggle the stream visibility.", 
                        "Discord Stream Overlay", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AppContext());
            }
        }
    }
}