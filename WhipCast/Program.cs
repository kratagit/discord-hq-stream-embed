using System;
using System.Threading;
using System.Windows.Forms;

namespace WhipCast
{
    static class Program
    {
        private static string appGuid = "whip-cast_SingleInstance_Mutex";

        [STAThread]
        static void Main()
        {
            using (Mutex mutex = new Mutex(false, "Global\\" + appGuid))
            {
                if (!mutex.WaitOne(0, false))
                {
                    MessageBox.Show(
                        "WhipCast is already running.\n\nPlease check your taskbar for the settings window, or use your configured hotkey to toggle the stream visibility.", 
                        "WhipCast", 
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