using System;
using System.Windows.Forms;
using AWSS3Sync.UI; // Added using statement

namespace AWSS3Sync // Root namespace can remain for Program.cs if desired
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmS3Sync()); // This will now correctly resolve to AWSS3Sync.UI.frmS3Sync
        }
    }
}
