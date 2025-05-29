using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using S3FileManager.Models;

namespace S3FileManager
{
    public partial class RoleSelectionForm : Form
    {
        public List<UserRole> SelectedRoles { get; private set; } = new List<UserRole>();

        public RoleSelectionForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(400, 300);
            this.Text = "Select Access Roles";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var titleLabel = new Label
            {
                Text = "Select which roles can access these files:",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(350, 30)
            };

            var userCheckBox = new CheckBox
            {
                Text = "User - Basic users can view and access these files",
                Location = new Point(30, 60),
                Size = new Size(330, 25),
                Name = "userCheckBox"
            };

            var executiveCheckBox = new CheckBox
            {
                Text = "Executive - Executives can download these files",
                Location = new Point(30, 90),
                Size = new Size(330, 25),
                Name = "executiveCheckBox",
                Checked = true
            };

            var adminCheckBox = new CheckBox
            {
                Text = "Administrator - Admins have full access (always selected)",
                Location = new Point(30, 120),
                Size = new Size(330, 25),
                Name = "adminCheckBox",
                Checked = true,
                Enabled = false
            };

            var noteLabel = new Label
            {
                Text = "Note: If applied to a folder, permissions will apply to all contents recursively.",
                ForeColor = Color.Gray,
                Location = new Point(30, 160),
                Size = new Size(330, 40)
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(230, 220),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(315, 220),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[]
            {
                titleLabel, userCheckBox, executiveCheckBox, adminCheckBox,
                noteLabel, okButton, cancelButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            SelectedRoles.Clear();

            var userCheckBox = this.Controls.Find("userCheckBox", false)[0] as CheckBox;
            var executiveCheckBox = this.Controls.Find("executiveCheckBox", false)[0] as CheckBox;

            if (userCheckBox?.Checked == true)
                SelectedRoles.Add(UserRole.User);

            if (executiveCheckBox?.Checked == true)
                SelectedRoles.Add(UserRole.Executive);

            // Administrator is always included
            SelectedRoles.Add(UserRole.Administrator);
        }
    }
}