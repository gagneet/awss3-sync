using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AWSS3Sync.Models;
using AWSS3Sync.Services;

namespace AWSS3Sync.Forms
{
    /// <summary>
    /// Unified login form that provides a single interface for both Cognito and local authentication
    /// </summary>
    public partial class UnifiedLoginForm : Form
    {
        private UnifiedAuthService? _authService;
        private TextBox? _usernameTextBox;
        private TextBox? _passwordTextBox;
        private CheckBox? _offlineModeCheckBox;
        // Placeholder for "Remember Me" functionality, to be implemented later.
        private CheckBox? _rememberMeCheckBox;
        private Button? _loginButton;
        private Label? _statusLabel;
        private ProgressBar? _progressBar;
        // Placeholder for a panel related to authentication methods, for future use.
        private Panel? _authMethodPanel;
        private RadioButton? _automaticModeRadio;
        private RadioButton? _cognitoOnlyRadio;
        private RadioButton? _localOnlyRadio;
        private Label? _authStatusLabel;
        
        public UnifiedUser? AuthenticatedUser { get; private set; }
        public AuthenticationResult? AuthResult { get; private set; }
        
        public UnifiedLoginForm()
        {
            InitializeComponent();
            InitializeAuthService();
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(480, 500);
            this.Text = "Strata S3 Manager - Unified Login";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // Title
            var titleLabel = new Label
            {
                Text = "Strata S3 File Manager",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Location = new Point(75, 20),
                Size = new Size(330, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            // Subtitle
            var subtitleLabel = new Label
            {
                Text = "Unified Authentication System",
                Font = new Font("Arial", 10, FontStyle.Italic),
                Location = new Point(75, 50),
                Size = new Size(330, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            };
            
            // Authentication method selection
            var methodGroupBox = new GroupBox
            {
                Text = "Authentication Method",
                Location = new Point(50, 80),
                Size = new Size(380, 80)
            };
            
            _automaticModeRadio = new RadioButton
            {
                Text = "Automatic (Recommended)",
                Location = new Point(15, 25),
                Size = new Size(160, 20),
                Checked = true
            };
            
            _cognitoOnlyRadio = new RadioButton
            {
                Text = "AWS Cognito Only",
                Location = new Point(15, 45),
                Size = new Size(130, 20)
            };
            
            _localOnlyRadio = new RadioButton
            {
                Text = "Local Only (Limited)",
                Location = new Point(200, 25),
                Size = new Size(150, 20)
            };
            
            methodGroupBox.Controls.AddRange(new Control[]
            {
                _automaticModeRadio, _cognitoOnlyRadio, _localOnlyRadio
            });
            
            // Credentials panel
            var credentialsGroupBox = new GroupBox
            {
                Text = "Credentials",
                Location = new Point(50, 170),
                Size = new Size(380, 120)
            };
            
            var usernameLabel = new Label
            {
                Text = "Username/Email:",
                Location = new Point(15, 25),
                Size = new Size(100, 20)
            };
            
            _usernameTextBox = new TextBox
            {
                Location = new Point(125, 23),
                Size = new Size(230, 25),
                TabIndex = 0
            };
            
            var passwordLabel = new Label
            {
                Text = "Password:",
                Location = new Point(15, 55),
                Size = new Size(100, 20)
            };
            
            _passwordTextBox = new TextBox
            {
                Location = new Point(125, 53),
                Size = new Size(230, 25),
                UseSystemPasswordChar = true,
                TabIndex = 1
            };
            
            _offlineModeCheckBox = new CheckBox
            {
                Text = "Try offline mode first (cached credentials)",
                Location = new Point(125, 83),
                Size = new Size(245, 20),
                TabIndex = 2
            };
            
            credentialsGroupBox.Controls.AddRange(new Control[]
            {
                usernameLabel, _usernameTextBox,
                passwordLabel, _passwordTextBox,
                _offlineModeCheckBox
            });
            
            // Status panel
            _authStatusLabel = new Label
            {
                Text = "Initializing authentication services...",
                Location = new Point(50, 300),
                Size = new Size(380, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Blue,
                Font = new Font("Arial", 9, FontStyle.Regular)
            };
            
            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(50, 325),
                Size = new Size(380, 20),
                ForeColor = Color.Blue,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            _progressBar = new ProgressBar
            {
                Location = new Point(50, 350),
                Size = new Size(380, 20),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };
            
            // Login button
            _loginButton = new Button
            {
                Text = "Login",
                Location = new Point(190, 380),
                Size = new Size(100, 35),
                TabIndex = 3,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            _loginButton.Click += LoginButton_Click;
            
            // Help link
            var helpLink = new LinkLabel
            {
                Text = "Need help?",
                Location = new Point(205, 425),
                Size = new Size(70, 20)
            };
            helpLink.LinkClicked += (s, e) =>
            {
                ShowHelpDialog();
            };
            
            this.Controls.AddRange(new Control[]
            {
                titleLabel, subtitleLabel, methodGroupBox, credentialsGroupBox,
                _authStatusLabel, _statusLabel, _progressBar,
                _loginButton, helpLink
            });
            
            this.AcceptButton = _loginButton;
            
            // Set focus to username
            this.Load += (s, e) => _usernameTextBox?.Focus();
        }
        
        private void InitializeAuthService()
        {
            try
            {
                _authService = new UnifiedAuthService();
                
                // Update authentication status
                if (_authStatusLabel != null)
                {
                    _authStatusLabel.Text = _authService.GetAuthenticationStatus();
                    
                    if (!_authService.IsCognitoAvailable)
                    {
                        _authStatusLabel.ForeColor = Color.Orange;
                        
                        // Disable Cognito-only option if not available
                        if (_cognitoOnlyRadio != null)
                        {
                            _cognitoOnlyRadio.Enabled = false;
                            _cognitoOnlyRadio.Text = "AWS Cognito Only (Unavailable)";
                        }
                        
                        // Select local-only if Cognito is not available
                        if (_localOnlyRadio != null && _automaticModeRadio != null)
                        {
                            _localOnlyRadio.Checked = true;
                            _automaticModeRadio.Checked = false;
                        }
                        
                        // Disable offline mode checkbox
                        if (_offlineModeCheckBox != null)
                        {
                            _offlineModeCheckBox.Enabled = false;
                            _offlineModeCheckBox.Text = "Offline mode (Cognito unavailable)";
                        }
                    }
                    else
                    {
                        _authStatusLabel.ForeColor = Color.Green;
                    }
                }
            }
            catch (Exception ex)
            {
                if (_authStatusLabel != null)
                {
                    _authStatusLabel.Text = $"Authentication initialization failed: {ex.Message}";
                    _authStatusLabel.ForeColor = Color.Red;
                }
                
                MessageBox.Show(
                    $"Failed to initialize authentication services: {ex.Message}\n\n" +
                    "The application may have limited functionality.",
                    "Initialization Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        
        private async void LoginButton_Click(object? sender, EventArgs e)
        {
            await HandleLoginAsync();
        }
        
        private async Task HandleLoginAsync()
        {
            if (string.IsNullOrWhiteSpace(_usernameTextBox?.Text))
            {
                MessageBox.Show("Please enter your username or email.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _usernameTextBox?.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(_passwordTextBox?.Text))
            {
                MessageBox.Show("Please enter your password.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _passwordTextBox?.Focus();
                return;
            }
            
            // Disable controls
            SetControlsEnabled(false);
            _statusLabel!.Text = "Authenticating...";
            _progressBar!.Visible = true;
            
            try
            {
                var preferOffline = _offlineModeCheckBox?.Checked ?? false;
                var allowLocalFallback = GetAllowLocalFallback();
                
                AuthResult = await _authService!.AuthenticateAsync(
                    _usernameTextBox.Text, 
                    _passwordTextBox.Text,
                    preferOffline,
                    allowLocalFallback);
                
                if (AuthResult.IsSuccess && AuthResult.User != null)
                {
                    AuthenticatedUser = AuthResult.User;
                    
                    // Show success message with method used
                    var methodDescription = GetMethodDescription(AuthResult.MethodUsed);
                    _statusLabel.Text = $"Authentication successful ({methodDescription})";
                    _statusLabel.ForeColor = Color.Green;
                    
                    // Show warnings if needed
                    if (AuthResult.RequiresAwsCredentialWarning && !string.IsNullOrEmpty(AuthResult.WarningMessage))
                    {
                        MessageBox.Show(
                            AuthResult.WarningMessage,
                            "Authentication Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    else if (!string.IsNullOrEmpty(AuthResult.WarningMessage))
                    {
                        MessageBox.Show(
                            AuthResult.WarningMessage,
                            "Authentication Notice",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    
                    await Task.Delay(500); // Brief delay to show success
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    _statusLabel.Text = "Authentication failed";
                    _statusLabel.ForeColor = Color.Red;
                    
                    MessageBox.Show(
                        AuthResult.ErrorMessage +
                        "\n\nTip: Try different authentication methods or contact your administrator.",
                        "Login Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Login error";
                _statusLabel.ForeColor = Color.Red;
                
                MessageBox.Show(
                    $"Unexpected login error: {ex.Message}",
                    "Login Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                SetControlsEnabled(true);
                _progressBar!.Visible = false;
            }
        }
        
        private bool GetAllowLocalFallback()
        {
            if (_automaticModeRadio?.Checked == true) return true;
            if (_localOnlyRadio?.Checked == true) return true;
            return false; // Cognito-only mode
        }
        
        private string GetMethodDescription(AuthenticationMethod method)
        {
            return method switch
            {
                AuthenticationMethod.CognitoOnline => "AWS Cognito",
                AuthenticationMethod.CognitoOffline => "Cached Credentials",
                AuthenticationMethod.Local => "Local Authentication",
                AuthenticationMethod.Fallback => "Local Fallback",
                _ => "Unknown"
            };
        }
        
        private void SetControlsEnabled(bool enabled)
        {
            _usernameTextBox!.Enabled = enabled;
            _passwordTextBox!.Enabled = enabled;
            _offlineModeCheckBox!.Enabled = enabled && _authService?.IsCognitoAvailable == true;
            _loginButton!.Enabled = enabled;
            _automaticModeRadio!.Enabled = enabled;
            _cognitoOnlyRadio!.Enabled = enabled && _authService?.IsCognitoAvailable == true;
            _localOnlyRadio!.Enabled = enabled;
        }
        
        private void ShowHelpDialog()
        {
            var helpText = "Unified Authentication Help:\n\n" +
                
                "Authentication Methods:\n" +
                "• Automatic: Try AWS Cognito first, fallback to local if needed\n" +
                "• AWS Cognito Only: Full AWS integration with proper permissions\n" +
                "• Local Only: Simple authentication with limited S3 access\n\n" +
                
                "Features:\n" +
                "• Offline Mode: Use cached Cognito credentials when offline\n" +
                "• Automatic Fallback: Seamlessly switch between methods\n" +
                "• Security Warnings: Clear indication of access limitations\n\n" +
                
                "Troubleshooting:\n" +
                "• Check your internet connection for AWS Cognito\n" +
                "• Use offline mode if you've logged in before\n" +
                "• Contact administrator for AWS Cognito setup\n" +
                "• Local authentication provides limited S3 access";
            
            MessageBox.Show(helpText, "Authentication Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _authService?.Dispose();
        }
    }
}