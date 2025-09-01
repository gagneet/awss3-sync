using System;
using System.Windows.Forms;
using S3FileManager.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show unified login form
            var loginForm = new UnifiedLoginForm();
            if (loginForm.ShowDialog() == DialogResult.OK && loginForm.AuthenticatedUser != null)
            {
                var user = loginForm.AuthenticatedUser;
                var authResult = loginForm.AuthResult;
                
                // Show security warnings if needed
                if (authResult?.RequiresAwsCredentialWarning == true)
                {
                    var warningMessage = $"Security Notice:\n\n{authResult.WarningMessage}\n\n" +
                        "Continue with limited access?";
                    
                    var result = MessageBox.Show(
                        warningMessage,
                        "Limited Access Warning",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    
                    if (result == DialogResult.No)
                    {
                        return; // User chose not to continue
                    }
                }
                
                // Route to appropriate main form based on user capabilities
                if (user.AuthType == AuthenticationType.Cognito && user.HasAwsCredentials)
                {
                    // User has full AWS capabilities - use optimized form
                    Application.Run(new OptimizedMainForm(UnifiedUserToCognitoUser(user)));
                }
                else if (user.AuthType == AuthenticationType.Local || !user.HasAwsCredentials)
                {
                    // User has limited capabilities - use basic form with warnings
                    var basicUser = UnifiedUserToBasicUser(user);
                    var mainForm = new MainForm(basicUser);
                    
                    // Show additional warning in main form if needed
                    if (user.IsLimitedAccess)
                    {
                        mainForm.Load += (s, e) =>
                        {
                            MessageBox.Show(
                                "You are running in limited access mode. Some S3 operations may not work.\n\n" +
                                "Contact your administrator to set up AWS Cognito authentication for full access.",
                                "Limited Access Mode",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        };
                    }
                    
                    Application.Run(mainForm);
                }
                else
                {
                    // Fallback to basic user
                    Application.Run(new MainForm(UnifiedUserToBasicUser(user)));
                }
            }
        }
        
        /// <summary>
        /// Convert UnifiedUser to CognitoUser for compatibility with OptimizedMainForm
        /// </summary>
        private static CognitoUser UnifiedUserToCognitoUser(UnifiedUser user)
        {
            return new CognitoUser
            {
                Username = user.Username,
                Email = user.Email,
                Sub = user.CognitoSub ?? string.Empty,
                Role = user.Role,
                Groups = user.Groups,
                LastLogin = user.LastLogin,
                TokenExpiry = user.TokenExpiry ?? DateTime.UtcNow.AddHours(1),
                AccessToken = user.AccessToken,
                IdToken = user.IdToken,
                RefreshToken = user.RefreshToken,
                AwsAccessKeyId = user.AwsAccessKeyId,
                AwsSecretAccessKey = user.AwsSecretAccessKey,
                AwsSessionToken = user.AwsSessionToken,
                IsOfflineMode = user.IsOfflineMode
            };
        }
        
        /// <summary>
        /// Convert UnifiedUser to basic User for compatibility with MainForm
        /// </summary>
        private static User UnifiedUserToBasicUser(UnifiedUser user)
        {
            return new User
            {
                Username = user.Username,
                Role = user.Role,
                LastLogin = user.LastLogin
            };
        }
    }
}