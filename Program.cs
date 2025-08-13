using System;
using System.Windows.Forms;
using S3FileManager.Forms;

namespace S3FileManager
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show enhanced login form with Cognito support
            var loginForm = new CognitoLoginForm();
            if (loginForm.ShowDialog() == DialogResult.OK)
            {
                if (loginForm.IsCognitoMode && loginForm.CognitoUser != null)
                {
                    // Use Cognito authenticated user
                    Application.Run(new OptimizedMainForm(loginForm.CognitoUser));
                }
                else if (loginForm.LegacyUser != null)
                {
                    // Use legacy user (backward compatibility)
                    Application.Run(new MainForm(loginForm.LegacyUser));
                }
            }
        }
    }
}