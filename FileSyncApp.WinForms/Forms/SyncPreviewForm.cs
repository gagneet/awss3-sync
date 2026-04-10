using Krypton.Toolkit;
using FileSyncApp.Core.Interfaces;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Shows a preview of planned sync operations so the user can review and deselect before executing.
/// </summary>
public class SyncPreviewForm : KryptonForm
{
    private readonly SyncPlan _plan;
    private DataGridView _grid = null!;
    private KryptonButton _btnOk = null!;
    private KryptonButton _btnCancel = null!;
    private KryptonLabel _lblSummary = null!;

    private const int ColCheck = 0;
    private const int ColAction = 1;
    private const int ColFile = 2;
    private const int ColLocalSize = 3;
    private const int ColLocalModified = 4;
    private const int ColRemoteSize = 5;
    private const int ColRemoteModified = 6;

    public SyncPreviewForm(SyncPlan plan)
    {
        _plan = plan;
        InitializeComponents();
        PopulateGrid();
    }

    private void InitializeComponents()
    {
        Text = "Sync Preview — Review Changes";
        Size = new Size(1000, 600);
        MinimumSize = new Size(800, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;

        _lblSummary = new KryptonLabel
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = BuildSummaryText(),
            StateCommon = { ShortText = { Font = new Font("Segoe UI", 9.5f) } }
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window
        };

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "Include",
            Width = 60,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = false
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File", FillWeight = 100, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Local Size", Width = 85, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Local Modified", Width = 130, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Remote Size", Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Remote Modified", Width = 130, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });

        _grid.CellFormatting += Grid_CellFormatting;

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50 };

        _btnOk = new KryptonButton
        {
            Text = "Start Sync",
            DialogResult = DialogResult.OK,
            Location = new Point(10, 10),
            Width = 120
        };
        _btnCancel = new KryptonButton
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(140, 10),
            Width = 100
        };

        var btnSelectAll = new KryptonButton { Text = "Select All", Location = new Point(260, 10), Width = 100 };
        var btnSelectNone = new KryptonButton { Text = "Deselect All", Location = new Point(370, 10), Width = 100 };

        btnSelectAll.Click += (s, e) => SetAllChecked(true);
        btnSelectNone.Click += (s, e) => SetAllChecked(false);

        buttonPanel.Controls.AddRange(new Control[] { _btnOk, _btnCancel, btnSelectAll, btnSelectNone });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        Controls.Add(_grid);
        Controls.Add(_lblSummary);
        Controls.Add(buttonPanel);
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();

        foreach (var op in _plan.Uploads)
            AddRow("⬆ Upload", op.LocalPath, op.Size, op.LocalModified, 0, default, true);

        foreach (var op in _plan.Downloads)
            AddRow("⬇ Download", string.IsNullOrEmpty(op.LocalPath) ? op.RemotePath : op.LocalPath,
                0, default, op.Size, op.RemoteModified, true);

        foreach (var op in _plan.LocalDeletes)
            AddRow("🗑 Del Local", op.LocalPath, op.Size, op.LocalModified, 0, default, false);

        foreach (var op in _plan.RemoteDeletes)
            AddRow("🗑 Del Remote", op.RemotePath, 0, default, op.Size, op.RemoteModified, false);

        foreach (var c in _plan.Conflicts)
            AddRow("⚠ Conflict", c.LocalPath, c.LocalSize, c.LocalModified, c.RemoteSize, c.RemoteModified, true);
    }

    private void AddRow(string action, string file, long localSize, DateTime localModified,
        long remoteSize, DateTime remoteModified, bool @checked)
    {
        var row = new DataGridViewRow();
        row.CreateCells(_grid,
            @checked,
            action,
            file,
            localSize > 0 ? FormatSize(localSize) : "",
            localModified != default ? localModified.ToString("g") : "",
            remoteSize > 0 ? FormatSize(remoteSize) : "",
            remoteModified != default ? remoteModified.ToString("g") : "");
        _grid.Rows.Add(row);
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColAction) return;

        var action = e.Value?.ToString() ?? "";
        e.CellStyle.ForeColor = action switch
        {
            string a when a.Contains("Upload") => Color.Blue,
            string a when a.Contains("Download") => Color.Green,
            string a when a.Contains("Del") => Color.Red,
            string a when a.Contains("Conflict") => Color.DarkOrange,
            _ => Color.Black
        };
        e.FormattingApplied = true;
    }

    private void SetAllChecked(bool value)
    {
        foreach (DataGridViewRow row in _grid.Rows)
            row.Cells[ColCheck].Value = value;
    }

    public SyncPlan GetSelectedPlan()
    {
        var selected = new SyncPlan();
        var uploadOps = _plan.Uploads.ToList();
        var downloadOps = _plan.Downloads.ToList();
        var localDelOps = _plan.LocalDeletes.ToList();
        var remoteDelOps = _plan.RemoteDeletes.ToList();
        var conflictOps = _plan.Conflicts.ToList();

        int uploadIdx = 0, downloadIdx = 0, localDelIdx = 0, remoteDelIdx = 0, conflictIdx = 0;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            bool included = row.Cells[ColCheck].Value is true;
            var action = row.Cells[ColAction].Value?.ToString() ?? "";

            if (action.Contains("Upload") && uploadIdx < uploadOps.Count)
            {
                if (included) selected.Uploads.Add(uploadOps[uploadIdx]);
                uploadIdx++;
            }
            else if (action.Contains("Download") && downloadIdx < downloadOps.Count)
            {
                if (included) selected.Downloads.Add(downloadOps[downloadIdx]);
                downloadIdx++;
            }
            else if (action.Contains("Del Local") && localDelIdx < localDelOps.Count)
            {
                if (included) selected.LocalDeletes.Add(localDelOps[localDelIdx]);
                localDelIdx++;
            }
            else if (action.Contains("Del Remote") && remoteDelIdx < remoteDelOps.Count)
            {
                if (included) selected.RemoteDeletes.Add(remoteDelOps[remoteDelIdx]);
                remoteDelIdx++;
            }
            else if (action.Contains("Conflict") && conflictIdx < conflictOps.Count)
            {
                if (included) selected.Conflicts.Add(conflictOps[conflictIdx]);
                conflictIdx++;
            }
        }

        return selected;
    }

    private string BuildSummaryText()
    {
        return $"  {_plan.Uploads.Count} uploads ({FormatSize(_plan.TotalUploadBytes)})  •  " +
               $"{_plan.Downloads.Count} downloads ({FormatSize(_plan.TotalDownloadBytes)})  •  " +
               $"{_plan.LocalDeletes.Count + _plan.RemoteDeletes.Count} deletes  •  " +
               $"{_plan.Conflicts.Count} conflicts";
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
