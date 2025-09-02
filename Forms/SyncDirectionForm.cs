using System;
using System.Drawing;
using System.Windows.Forms;

namespace AWSS3Sync.Forms
{
    public enum SyncDirection
    {
        None,
        S3ToLocal,
        LocalToS3
    }

    public partial class SyncDirectionForm : Form
    {
        public SyncDirection SelectedDirection { get; private set; } = SyncDirection.None;

        public SyncDirectionForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Select Sync Direction";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var infoLabel = new Label
            {
                Text = "Please choose the direction for synchronization.",
                Location = new Point(20, 20),
                Size = new Size(360, 30)
            };

            var s3ToLocalButton = new Button
            {
                Text = "S3 to Local",
                DialogResult = DialogResult.OK,
                Location = new Point(40, 70),
                Size = new Size(150, 40)
            };
            s3ToLocalButton.Click += (sender, e) =>
            {
                SelectedDirection = SyncDirection.S3ToLocal;
                this.Close();
            };

            var localToS3Button = new Button
            {
                Text = "Local to S3",
                DialogResult = DialogResult.OK,
                Location = new Point(210, 70),
                Size = new Size(150, 40)
            };
            localToS3Button.Click += (sender, e) =>
            {
                SelectedDirection = SyncDirection.LocalToS3;
                this.Close();
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(150, 120),
                Size = new Size(100, 30)
            };
            cancelButton.Click += (sender, e) => this.Close();


            this.Controls.AddRange(new Control[] { infoLabel, s3ToLocalButton, localToS3Button, cancelButton });
            this.CancelButton = cancelButton;
        }
    }
}
