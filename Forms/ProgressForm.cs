using System.Drawing;
using System.Windows.Forms;

namespace S3FileManager
{
    public class ProgressForm : Form
    {
        private Label label = null!;
        private ProgressBar progressBar = null!;

        public ProgressForm(string message)
        {
            InitializeComponent();
            label.Text = message;
        }

        private void InitializeComponent()
        {
            this.Size = new Size(400, 150);
            this.Text = "Progress";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;

            label = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(350, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 10)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(20, 60),
                Size = new Size(350, 30),
                Style = ProgressBarStyle.Marquee
            };

            this.Controls.Add(label);
            this.Controls.Add(progressBar);
        }

        public void UpdateMessage(string message)
        {
            if (label.InvokeRequired)
            {
                label.Invoke(new System.Action(() => label.Text = message));
            }
            else
            {
                label.Text = message;
            }
        }
    }
}