using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using S3FileManager.Models;
using S3FileManager.Services;

namespace S3FileManager.Forms
{
    public partial class CognitoLoginForm : Form
    {
        private CognitoAuthService? _authService;
        private TextBox? _usernameTextBox;
        private TextBox? _passwordTextBox;
        private ComboBox? _roleComboBox;
        private CheckBox? _offlineModeCheckBox;
        private CheckBox? _rememberMeCheckBox;
        private Button? _loginButton;
        private Label? _statusLabel;
        private ProgressBar? _progressBar;
        private Panel? _cognitoPanel;
        private Panel? _legacyPanel;
        private RadioButton? _cognitoModeRadio;
        private RadioButton? _legacyModeRadio;
        
        public CognitoUser? CognitoUser { get; private set; }
        public User? LegacyUser { get; private set; }
        public bool IsCognitoMode { get; private set; }
        
        public CognitoLoginForm()
        {
            InitializeComponent();
            InitializeAuthService();
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(450, 420);
            this.Text = "Strata S3 Manager - Login";
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
                Size = new Size(300, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            // Authentication mode selection
            var modeGroupBox = new GroupBox
            {
                Text = "Authentication Mode",
                Location = new Point(50, 60),
                Size = new Size(350, 60)
            };
            
            _cognitoModeRadio = new RadioButton
            {
                Text = "AWS Cognito (Recommended)",
                Location = new Point(10, 25),
                Size = new Size(160, 20),
                Checked = true
            };
            _cognitoModeRadio.CheckedChanged += ModeRadio_CheckedChanged;
            
            _legacyModeRadio = new RadioButton
            {
                Text = "Legacy Mode",
                Location = new Point(180, 25),
                Size = new Size(150, 20)
            };
            
            modeGroupBox.Controls.Add(_cognitoModeRadio);
            modeGroupBox.Controls.Add(_legacyModeRadio);
            
            // Cognito login panel
            _cognitoPanel = new Panel
            {
                Location = new Point(50, 130),
                Size = new Size(350, 180)
            };
            
            var cognitoUsernameLabel = new Label
            {
                Text = "Username/Email:",
                Location = new Point(0, 10),
                Size = new Size(100, 20)
            };
            
            _usernameTextBox = new TextBox
            {
                Location = new Point(110, 8),
                Size = new Size(200, 25),
                TabIndex = 0
            };
            
            var passwordLabel = new Label
            {
                Text = "Password:",
                Location = new Point(0, 40),
                Size = new Size(100, 20)
            };
            
            _passwordTextBox = new TextBox
            {
                Location = new Point(110, 38),
                Size = new Size(200, 25),
                UseSystemPasswordChar = true,
                TabIndex = 1
            };
            
            _offlineModeCheckBox = new CheckBox
            {
                Text = "Offline Mode (Use cached credentials)",
                Location = new Point(110, 70),
                Size = new Size(230, 20),
                TabIndex = 2
            };
            
            _rememberMeCheckBox = new CheckBox
            {
                Text = "Remember me (7 days)",
                Location = new Point(110, 95),
                Size = new Size(200, 20),
                Checked = true,
                TabIndex = 3
            };
            
            _statusLabel = new Label
            {
                Text = "",
                Location = new Point(0, 125),
                Size = new Size(350, 20),
                ForeColor = Color.Blue,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            _progressBar = new ProgressBar
            {
                Location = new Point(0, 150),
                Size = new Size(350, 20),
                Visible = false,
                Style = ProgressBarStyle.Marquee
            };
            
            _cognitoPanel.Controls.AddRange(new Control[]
            {
                cognitoUsernameLabel, _usernameTextBox,
                passwordLabel, _passwordTextBox,
                _offlineModeCheckBox, _rememberMeCheckBox,
                _statusLabel, _progressBar
            });
            
            // Legacy login panel
            _legacyPanel = new Panel
            {
                Location = new Point(50, 130),
                Size = new Size(350, 180),
                Visible = false
            };
            
            var legacyUsernameLabel = new Label
            {
                Text = "Username:",
                Location = new Point(0, 10),
                Size = new Size(100, 20)
            };
            
            var legacyUsernameTextBox = new TextBox
            {
                Name = "legacyUsernameTextBox",
                Location = new Point(110, 8),
                Size = new Size(200, 25)
            };
            
            var roleLabel = new Label
            {
                Text = "Role:",
                Location = new Point(0, 40),
                Size = new Size(100, 20)
            };
            
            _roleComboBox = new ComboBox
            {
                Location = new Point(110, 38),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _roleComboBox.Items.AddRange(new[] { "User", "Executive", "Administrator" });
            _roleComboBox.SelectedIndex = 0;
            
            var legacyNote = new Label
            {
                Text = "Note: Legacy mode does not support AWS IAM authentication",
                Location = new Point(0, 70),
                Size = new Size(350, 40),
                ForeColor = Color.Gray,
                Font = new Font("Arial", 8, FontStyle.Italic)
            };
            
            _legacyPanel.Controls.AddRange(new Control[]
            {
                legacyUsernameLabel, legacyUsernameTextBox,
                roleLabel, _roleComboBox,
                legacyNote
            });
            
            // Login button
            _loginButton = new Button
            {
                Text = "Login",
                Location = new Point(175, 320),
                Size = new Size(100, 35),
                TabIndex = 4
            };
            _loginButton.Click += LoginButton_Click;
            
            // Help link
            var helpLink = new LinkLabel
            {
                Text = "Need help?",
                Location = new Point(190, 365),
                Size = new Size(70, 20)
            };
            helpLink.LinkClicked += (s, e) =>
            {
                MessageBox.Show(
                    "AWS Cognito Mode:\n" +
                    "• Use your AWS Cognito credentials\n" +
                    "• Supports offline access with cached credentials\n" +
                    "• Automatically syncs role permissions from AWS\n\n" +
                    "Legacy Mode:\n" +
                    "• Simple username-based authentication\n" +
                    "• Manual role selection\n" +
                    "• No AWS IAM integration\n\n" +
                    "Contact your administrator for credentials.",
                    "Login Help",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            
            this.Controls.AddRange(new Control[]
            {
                titleLabel, modeGroupBox,
                _cognitoPanel, _legacyPanel,
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
                _authService = new CognitoAuthService();
                
                // Check for offline mode availability
                var config = ConfigurationService.GetConfiguration();
                if (!config.Cognito.EnableOfflineMode && _offlineModeCheckBox != null)
                {
                    _offlineModeCheckBox.Enabled = false;
                    _offlineModeCheckBox.Text = "Offline Mode (Disabled by admin)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize authentication service: {ex.Message}\n\n" +
                    "Falling back to legacy mode.",
                    "Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                
                // Force legacy mode
                if (_legacyModeRadio != null)
                {
                    _legacyModeRadio.Checked = true;
                    _cognitoModeRadio!.Enabled = false;
                }
            }
        }
        
        private void ModeRadio_CheckedChanged(object? sender, EventArgs e)
        {
            if (_cognitoModeRadio?.Checked == true)
            {
                _cognitoPanel!.Visible = true;
                _legacyPanel!.Visible = false;
                IsCognitoMode = true;
                _usernameTextBox?.Focus();
            }
            else
            {
                _cognitoPanel!.Visible = false;
                _legacyPanel!.Visible = true;
                IsCognitoMode = false;
                var legacyUsername = _legacyPanel!.Controls.Find("legacyUsernameTextBox", false)[0];
                legacyUsername?.Focus();
            }
        }
        
        private async void LoginButton_Click(object? sender, EventArgs e)
        {
            if (IsCognitoMode)
            {
                await HandleCognitoLogin();
            }
            else
            {
                HandleLegacyLogin();
            }
        }
        
        private async Task HandleCognitoLogin()
        {
            if (string.IsNullOrWhiteSpace(_usernameTextBox?.Text))
            {
                MessageBox.Show("Please enter your username or email.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(_passwordTextBox?.Text))
            {
                MessageBox.Show("Please enter your password.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // Disable controls
            SetControlsEnabled(false);
            _statusLabel!.Text = "Authenticating...";
            _progressBar!.Visible = true;
            
            try
            {
                // Attempt authentication
                var forceOnline = !(_offlineModeCheckBox?.Checked ?? false);
                CognitoUser = await _authService!.AuthenticateAsync(
                    _usernameTextBox.Text, 
                    _passwordTextBox.Text,
                    forceOnline);
                
                if (CognitoUser != null)
                {
                    // Show success message
                    _statusLabel.Text = CognitoUser.IsOfflineMode 
                        ? "Authenticated (Offline Mode)" 
                        : "Authentication successful!";
                    _statusLabel.ForeColor = Color.Green;
                    
                    await Task.Delay(500); // Brief delay to show success
                    
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    _statusLabel.Text = "Authentication failed";
                    _statusLabel.ForeColor = Color.Red;
                    MessageBox.Show(
                        "Invalid username or password.\n\n" +
                        "If you're having trouble, try:\n" +
                        "• Checking your internet connection\n" +
                        "• Using offline mode if you've logged in before\n" +
                        "• Contacting your administrator",
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
                    $"Login failed: {ex.Message}\n\n" +
                    "You can try:\n" +
                    "• Using offline mode if available\n" +
                    "• Switching to legacy mode\n" +
                    "• Checking your network connection",
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
        
        private void HandleLegacyLogin()
        {
            var usernameTextBox = _legacyPanel!.Controls.Find("legacyUsernameTextBox", false)[0] as TextBox;
            
            if (string.IsNullOrWhiteSpace(usernameTextBox?.Text))
            {
                MessageBox.Show("Please enter a username.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_roleComboBox?.SelectedItem == null)
            {
                MessageBox.Show("Please select a role.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            LegacyUser = new User
            {
                Username = usernameTextBox.Text,
                Role = Enum.Parse<UserRole>(_roleComboBox.SelectedItem.ToString() ?? "User"),
                LastLogin = DateTime.Now
            };
            
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
        private void SetControlsEnabled(bool enabled)
        {
            _usernameTextBox!.Enabled = enabled;
            _passwordTextBox!.Enabled = enabled;
            _offlineModeCheckBox!.Enabled = enabled;
            _rememberMeCheckBox!.Enabled = enabled;
            _loginButton!.Enabled = enabled;
            _cognitoModeRadio!.Enabled = enabled;
            _legacyModeRadio!.Enabled = enabled;
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _authService?.Dispose();
        }
    }
}