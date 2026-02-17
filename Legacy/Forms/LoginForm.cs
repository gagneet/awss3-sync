using System;
using System.Drawing;
using System.Windows.Forms;
using AWSS3Sync.Models;
using AWSS3Sync.Services;

namespace AWSS3Sync
{
    public partial class LoginForm : Form
    {
        public User? CurrentUser { get; private set; }
        private readonly UserService _userService;

        public LoginForm()
        {
            _userService = new UserService();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(400, 300);
            this.Text = "S3 File Manager - Login";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "S3 File Manager",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Location = new Point(50, 30),
                Size = new Size(300, 30),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var usernameLabel = new Label
            {
                Text = "Username:",
                Location = new Point(50, 80),
                Size = new Size(80, 20)
            };

            var usernameTextBox = new TextBox
            {
                Name = "usernameTextBox",
                Location = new Point(150, 78),
                Size = new Size(150, 25)
            };

            var passwordLabel = new Label
            {
                Text = "Password:",
                Location = new Point(50, 120),
                Size = new Size(80, 20)
            };

            var passwordTextBox = new TextBox
            {
                Name = "passwordTextBox",
                Location = new Point(150, 118),
                Size = new Size(150, 25),
                PasswordChar = '*'
            };

            var loginButton = new Button
            {
                Text = "Login",
                Location = new Point(150, 180),
                Size = new Size(100, 30)
            };
            loginButton.Click += LoginButton_Click;

            this.Controls.AddRange(new Control[]
            {
                titleLabel, usernameLabel, usernameTextBox,
                passwordLabel, passwordTextBox, loginButton
            });

            this.AcceptButton = loginButton;
        }

        private void LoginButton_Click(object? sender, EventArgs e)
        {
            var usernameTextBox = this.Controls.Find("usernameTextBox", false)[0] as TextBox;
            var passwordTextBox = this.Controls.Find("passwordTextBox", false)[0] as TextBox;

            if (usernameTextBox == null || string.IsNullOrWhiteSpace(usernameTextBox.Text))
            {
                MessageBox.Show("Please enter a username.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (passwordTextBox == null || string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                MessageBox.Show("Please enter a password.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentUser = _userService.ValidateUser(usernameTextBox.Text, passwordTextBox.Text);

            if (CurrentUser != null)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Invalid username or password.", "Login Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}