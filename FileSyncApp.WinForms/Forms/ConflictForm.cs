using FileSyncApp.Core.Models;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

public partial class ConflictForm : KryptonForm
{
    public SyncActionType SelectedAction { get; private set; } = SyncActionType.Skip;

    public ConflictForm(string path, FileNode local, FileNode remote)
    {
        InitializeComponent(path, local, remote);
    }

    private void InitializeComponent(string path, FileNode local, FileNode remote)
    {
        this.Text = "Conflict Detected";
        this.Size = new Size(600, 300);
        this.StartPosition = FormStartPosition.CenterParent;

        var panel = new KryptonPanel { Dock = DockStyle.Fill };

        var lblInfo = new KryptonLabel
        {
            Text = $"Conflict detected for: {path}\n" +
                   $"Local:  {FormatFileSize(local.Size)}, {local.LastModified}\n" +
                   $"Remote: {FormatFileSize(remote.Size)}, {remote.LastModified}",
            Location = new Point(20, 20),
            Width = 550,
            Height = 100
        };

        var btnLocal = new KryptonButton { Text = "Keep Local", Location = new Point(30, 150), Width = 120 };
        btnLocal.Click += (s, e) => { SelectedAction = SyncActionType.Upload; DialogResult = DialogResult.OK; };

        var btnRemote = new KryptonButton { Text = "Keep Remote", Location = new Point(160, 150), Width = 120 };
        btnRemote.Click += (s, e) => { SelectedAction = SyncActionType.Download; DialogResult = DialogResult.OK; };

        var btnBoth = new KryptonButton { Text = "Keep Both", Location = new Point(290, 150), Width = 120 };
        btnBoth.Click += (s, e) => { SelectedAction = SyncActionType.KeepBoth; DialogResult = DialogResult.OK; };

        var btnSkip = new KryptonButton { Text = "Skip", Location = new Point(420, 150), Width = 120 };
        btnSkip.Click += (s, e) => { SelectedAction = SyncActionType.Skip; DialogResult = DialogResult.OK; };

        panel.Controls.AddRange(new Control[] { lblInfo, btnLocal, btnRemote, btnBoth, btnSkip });
        this.Controls.Add(panel);
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
