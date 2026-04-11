using FileSyncApp.Core.Interfaces;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Bulk conflict-resolution dialog. Shows all conflicted files in a grid and lets
/// the user pick a resolution per file, or apply a quick policy to all rows.
/// </summary>
public sealed class ConflictResolutionForm : KryptonForm
{
    private readonly List<ConflictInfo> _conflicts;
    private DataGridView _grid = null!;

    public ConflictResolutionForm(List<ConflictInfo> conflicts)
    {
        _conflicts = conflicts;
        BuildUI();
        LoadRows();
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region UI construction

    private void BuildUI()
    {
        Text            = "Resolve Sync Conflicts";
        Width           = 900;
        Height          = 560;
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;

        // ── Header label ────────────────────────────────────────────────
        var lblHeader = new KryptonLabel
        {
            Text     = $"⚠  {_conflicts.Count} file(s) modified on both sides — choose a resolution for each:",
            Location = new Point(12, 14),
            Width    = 860,
            Height   = 22
        };
        lblHeader.StateCommon.ShortText.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        // ── Quick-apply buttons ─────────────────────────────────────────
        var btnNewerWins   = QuickBtn("⏱  Newer Wins",    Color.FromArgb(0, 120, 215));
        var btnLocalAll    = QuickBtn("📁  All Local",     Color.FromArgb(30, 120, 0));
        var btnRemoteAll   = QuickBtn("☁  All Remote",    Color.FromArgb(0, 90, 160));
        var btnSkipAll     = QuickBtn("⏭  Skip All",      Color.FromArgb(90, 90, 90));

        int bx = 12;
        foreach (var btn in new[] { btnNewerWins, btnLocalAll, btnRemoteAll, btnSkipAll })
        {
            btn.Location = new Point(bx, 42);
            btn.Size     = new Size(130, 30);
            bx          += 138;
        }

        btnNewerWins.Click += (_, _) => ApplyAll(r => r.LocalModified >= r.RemoteModified ? "Keep Local" : "Keep Remote");
        btnLocalAll.Click  += (_, _) => ApplyAll(_ => "Keep Local");
        btnRemoteAll.Click += (_, _) => ApplyAll(_ => "Keep Remote");
        btnSkipAll.Click   += (_, _) => ApplyAll(_ => "Skip");

        // ── Grid ────────────────────────────────────────────────────────
        _grid = new DataGridView
        {
            Location            = new Point(12, 82),
            Size                = new Size(860, 370),
            AutoGenerateColumns = false,
            AllowUserToAddRows  = false,
            AllowUserToDeleteRows = false,
            ReadOnly            = false,
            SelectionMode       = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible   = false,
            BorderStyle         = BorderStyle.None,
            BackgroundColor     = Color.White,
            GridColor           = Color.FromArgb(220, 220, 220),
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 28
        };
        _grid.DefaultCellStyle.SelectionBackColor    = Color.FromArgb(180, 215, 245);
        _grid.DefaultCellStyle.SelectionForeColor    = Color.Black;
        _grid.ColumnHeadersDefaultCellStyle.Font     = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _grid.DefaultCellStyle.Font                  = new Font("Segoe UI", 8.5f);

        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn
            {
                HeaderText = "File", Width = 220, DataPropertyName = "FileName", ReadOnly = true
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Local Modified", Width = 130, DataPropertyName = "LocalModifiedStr", ReadOnly = true
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Local Size", Width = 80, DataPropertyName = "LocalSizeStr", ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Remote Modified", Width = 130, DataPropertyName = "RemoteModifiedStr", ReadOnly = true
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Remote Size", Width = 80, DataPropertyName = "RemoteSizeStr", ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Newer", Width = 60, DataPropertyName = "NewerSide", ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            },
            new DataGridViewComboBoxColumn
            {
                HeaderText = "Resolution", Width = 140, DataPropertyName = "Resolution",
                Items      = { "Keep Local", "Keep Remote", "Keep Both", "Skip" },
                FlatStyle  = FlatStyle.Flat
            }
        });

        _grid.CellFormatting += OnCellFormatting;

        // ── Bottom buttons ───────────────────────────────────────────────
        var btnApply = new KryptonButton
        {
            Text     = "Apply Resolutions",
            Location = new Point(680, 464),
            Size     = new Size(150, 34),
        };
        btnApply.StateCommon.Back.Color1 = Color.FromArgb(0, 122, 204);
        btnApply.StateCommon.Content.ShortText.Color1 = Color.White;
        btnApply.Click += (_, _) => { WriteResolutions(); DialogResult = DialogResult.OK; };

        var btnCancel = new KryptonButton
        {
            Text     = "Cancel",
            Location = new Point(524, 464),
            Size     = new Size(148, 34)
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; };

        Controls.AddRange(new Control[]
        {
            lblHeader,
            btnNewerWins, btnLocalAll, btnRemoteAll, btnSkipAll,
            _grid, btnApply, btnCancel
        });

        AcceptButton = btnApply;
        CancelButton = btnCancel;
    }

    private static KryptonButton QuickBtn(string text, Color back)
    {
        var b = new KryptonButton { Text = text };
        b.StateCommon.Back.Color1 = back;
        b.StateCommon.Content.ShortText.Color1 = Color.White;
        return b;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Data

    private void LoadRows()
    {
        _grid.DataSource = _conflicts.Select(c => new ConflictRow(c)).ToList();
    }

    private void ApplyAll(Func<ConflictRow, string> picker)
    {
        if (_grid.DataSource is not List<ConflictRow> rows) return;
        foreach (var row in rows) row.Resolution = picker(row);
        // Reset DataSource to force the grid to re-read all cell values
        // (List<T> has no change notification, so Refresh() alone is insufficient)
        _grid.DataSource = null;
        _grid.DataSource = rows;
    }

    private void WriteResolutions()
    {
        if (_grid.DataSource is not List<ConflictRow> rows) return;

        // Commit any in-progress combobox edit before reading
        _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

        foreach (var row in rows)
            row.Conflict.Resolution = row.Resolution switch
            {
                "Keep Local"  => ConflictResolution.KeepLocal,
                "Keep Remote" => ConflictResolution.KeepRemote,
                "Keep Both"   => ConflictResolution.KeepBoth,
                _             => ConflictResolution.Skip
            };
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Formatting

    private static void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid) return;
        if (grid.DataSource is not List<ConflictRow> rows) return;
        if (e.RowIndex < 0 || e.RowIndex >= rows.Count) return;

        var row = rows[e.RowIndex];

        // Green tint on the newer side columns
        if (row.LocalModified >= row.RemoteModified)
        {
            if (e.ColumnIndex == 1 || e.ColumnIndex == 2)
                e.CellStyle.BackColor = Color.FromArgb(210, 245, 210);
        }
        else
        {
            if (e.ColumnIndex == 3 || e.ColumnIndex == 4)
                e.CellStyle.BackColor = Color.FromArgb(210, 245, 210);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    #region Row view-model

    public sealed class ConflictRow
    {
        private static readonly string[] _sizes = { "B", "KB", "MB", "GB", "TB" };

        public ConflictInfo Conflict        { get; }
        public string       FileName        { get; } = "";
        public DateTime     LocalModified   { get; }
        public DateTime     RemoteModified  { get; }
        public string       LocalModifiedStr  { get; }
        public string       RemoteModifiedStr { get; }
        public string       LocalSizeStr    { get; }
        public string       RemoteSizeStr   { get; }
        public string       NewerSide       { get; }
        public string       Resolution      { get; set; } = "Keep Local";

        public ConflictRow(ConflictInfo c)
        {
            Conflict          = c;
            FileName          = Path.GetFileName(c.LocalPath);
            LocalModified     = c.LocalModified;
            RemoteModified    = c.RemoteModified;
            LocalModifiedStr  = c.LocalModified.ToString("g");
            RemoteModifiedStr = c.RemoteModified.ToString("g");
            LocalSizeStr      = FormatSize(c.LocalSize);
            RemoteSizeStr     = FormatSize(c.RemoteSize);
            NewerSide         = c.LocalModified >= c.RemoteModified ? "Local" : "Remote";
            // Default to newer wins
            Resolution        = c.LocalModified >= c.RemoteModified ? "Keep Local" : "Keep Remote";
        }

        private static string FormatSize(long bytes)
        {
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < _sizes.Length - 1) { order++; size /= 1024; }
            return $"{size:0.#} {_sizes[order]}";
        }
    }

    #endregion
}
