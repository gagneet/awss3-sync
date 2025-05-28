using System;
using System.Drawing;
using System.Windows.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public partial class LoginForm : Form
    {
        public User CurrentUser { get; private set; }

        public LoginForm()
        {
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

            var roleLabel = new Label
            {
                Text = "Role:",
                Location = new Point(50, 120),
                Size = new Size(80, 20)
            };

            var roleComboBox = new ComboBox
            {
                Name = "roleComboBox",
                Location = new Point(150, 118),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            roleComboBox.Items.AddRange(new[] { "User", "Executive", "Administrator" });
            roleComboBox.SelectedIndex = 0;

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
                roleLabel, roleComboBox, loginButton
            });

            this.AcceptButton = loginButton;
        }

        private void LoginButton_Click(object sender, EventArgs e)
        {
            var usernameTextBox = this.Controls.Find("usernameTextBox", false)[0] as TextBox;
            var roleComboBox = this.Controls.Find("roleComboBox", false)[0] as ComboBox;

            if (string.IsNullOrWhiteSpace(usernameTextBox.Text))
            {
                MessageBox.Show("Please enter a username.", "Login Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CurrentUser = new User
            {
                Username = usernameTextBox.Text,
                Role = Enum.Parse<UserRole>(roleComboBox.SelectedItem.ToString()),
                LastLogin = DateTime.Now
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}