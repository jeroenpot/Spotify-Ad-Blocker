using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EZBlocker
{
    internal static class Program
    {
        public static string appGuid =
            ((GuidAttribute)
                Assembly.GetExecutingAssembly().GetCustomAttributes(typeof (GuidAttribute), false).GetValue(0)).Value;

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            var mutexId = string.Format("Local\\{{{0}}}", appGuid); // unique id for local mutex

            using (var mutex = new Mutex(false, mutexId))
            {
                if (mutex.WaitOne(TimeSpan.Zero))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainWindow());
                    mutex.ReleaseMutex();
                }
                else // another instance is already running
                {
                    WindowUtilities.ShowFirstInstance();
                }
            }
        }
    }
}