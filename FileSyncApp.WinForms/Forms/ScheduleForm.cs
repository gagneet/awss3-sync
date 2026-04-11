using Krypton.Toolkit;
using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Manage scheduled background sync jobs.
/// </summary>
public sealed class ScheduleForm : KryptonForm
{
    private readonly IConfigurationService _configService;
    private ISyncSchedulerService?         _scheduler;

    private DataGridView _grid        = null!;
    private Button       _btnAdd      = null!;
    private Button       _btnEdit     = null!;
    private Button       _btnDelete   = null!;
    private Button       _btnToggle   = null!;
    private Button       _btnClose    = null!;
    private Label        _lblStatus   = null!;

    private List<SyncSchedule> _schedules = new();

    public ScheduleForm(IConfigurationService configService, ISyncSchedulerService? scheduler = null)
    {
        _configService = configService;
        _scheduler     = scheduler;
        BuildUI();
        LoadSchedules();
    }

    // ── UI construction ───────────────────────────────────────────────────

    private void BuildUI()
    {
        Text            = "Scheduled Sync Jobs";
        Size            = new Size(820, 480);
        MinimumSize     = new Size(700, 380);
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        PaletteMode     = PaletteMode.Office2010Black;

        // Grid
        _grid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
            BackgroundColor       = Color.FromArgb(45, 45, 48),
            ForeColor             = Color.White,
            GridColor             = Color.FromArgb(70, 70, 75),
            BorderStyle           = BorderStyle.None,
            RowHeadersVisible     = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
        };
        _grid.DefaultCellStyle.BackColor      = Color.FromArgb(45, 45, 48);
        _grid.DefaultCellStyle.ForeColor      = Color.White;
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 122, 204);
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.EnableHeadersVisualStyles = false;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="colName",    HeaderText="Name",         FillWeight=25 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="colLocal",   HeaderText="Local Path",   FillWeight=30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="colRemote",  HeaderText="S3 Prefix",    FillWeight=15 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="colCron",    HeaderText="Cron",         FillWeight=15 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name="colNext",    HeaderText="Next Run",     FillWeight=15 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn{ Name="colEnabled", HeaderText="Enabled",      FillWeight=8, ReadOnly=true });
        _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditSelected(); };

        // Button panel
        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 48 };
        btnPanel.BackColor = Color.FromArgb(30, 30, 30);

        _btnAdd    = MakeButton("➕ Add",     Color.FromArgb(30, 120, 0));
        _btnEdit   = MakeButton("✏ Edit",    Color.FromArgb(60, 100, 160));
        _btnDelete = MakeButton("🗑 Delete",  Color.FromArgb(160, 40, 40));
        _btnToggle = MakeButton("⏸ Enable/Disable", Color.FromArgb(90, 70, 0));
        _btnClose  = MakeButton("✕ Close",   Color.FromArgb(70, 70, 80));

        int bx = 8;
        foreach (var b in new[] { _btnAdd, _btnEdit, _btnDelete, _btnToggle })
        {
            b.Location = new Point(bx, 8); b.Size = new Size(130, 32); bx += 136;
        }
        _btnClose.Location = new Point(btnPanel.Width - 116, 8);
        _btnClose.Size     = new Size(108, 32);
        _btnClose.Anchor   = AnchorStyles.Right | AnchorStyles.Top;

        btnPanel.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnToggle, _btnClose });

        // Status label
        _lblStatus = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            ForeColor = Color.LightGray,
            BackColor = Color.FromArgb(25, 25, 25),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(4, 0, 0, 0)
        };

        Controls.Add(_grid);
        Controls.Add(btnPanel);
        Controls.Add(_lblStatus);

        _btnAdd.Click    += (s, e) => AddNew();
        _btnEdit.Click   += (s, e) => EditSelected();
        _btnDelete.Click += (s, e) => DeleteSelected();
        _btnToggle.Click += (s, e) => ToggleSelected();
        _btnClose.Click  += (s, e) => Close();
    }

    private static Button MakeButton(string text, Color back) =>
        new Button
        {
            Text      = text,
            BackColor = back,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f),
        };

    // ── Data ──────────────────────────────────────────────────────────────

    private void LoadSchedules()
    {
        _schedules = _configService.GetSchedules();
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var s in _schedules)
        {
            _grid.Rows.Add(
                s.Name,
                s.LocalPath,
                s.RemotePrefix,
                s.CronExpression,
                s.NextRun?.ToString("g") ?? "—",
                s.IsEnabled);
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int enabled = _schedules.Count(s => s.IsEnabled);
        _lblStatus.Text = $"  {_schedules.Count} schedule(s), {enabled} enabled" +
            (_scheduler?.IsRunning == true ? "  •  Scheduler running" : "  •  Scheduler not started");
    }

    // ── Actions ───────────────────────────────────────────────────────────

    private void AddNew()
    {
        using var dlg = new ScheduleEditDialog(null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _configService.SaveSchedule(dlg.Result!);
        _scheduler?.AddOrUpdateScheduleAsync(dlg.Result!);
        LoadSchedules();
    }

    private void EditSelected()
    {
        var s = SelectedSchedule(); if (s is null) return;
        using var dlg = new ScheduleEditDialog(s);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _configService.SaveSchedule(dlg.Result!);
        _scheduler?.AddOrUpdateScheduleAsync(dlg.Result!);
        LoadSchedules();
    }

    private void DeleteSelected()
    {
        var s = SelectedSchedule(); if (s is null) return;
        if (MessageBox.Show($"Delete schedule '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes) return;

        _configService.DeleteSchedule(s.Id);
        _scheduler?.RemoveScheduleAsync(s.Id);
        LoadSchedules();
    }

    private void ToggleSelected()
    {
        var s = SelectedSchedule(); if (s is null) return;
        s.IsEnabled = !s.IsEnabled;
        _configService.SaveSchedule(s);
        _ = s.IsEnabled
            ? _scheduler?.AddOrUpdateScheduleAsync(s)
            : _scheduler?.RemoveScheduleAsync(s.Id);
        LoadSchedules();
    }

    private SyncSchedule? SelectedSchedule()
    {
        int idx = _grid.CurrentRow?.Index ?? -1;
        return idx >= 0 && idx < _schedules.Count ? _schedules[idx] : null;
    }
}

// ── Inline edit dialog ────────────────────────────────────────────────────

internal sealed class ScheduleEditDialog : Form
{
    public SyncSchedule? Result { get; private set; }

    private readonly TextBox _txtName    = new() { Width = 260 };
    private readonly TextBox _txtLocal   = new() { Width = 260 };
    private readonly TextBox _txtRemote  = new() { Width = 260 };
    private readonly TextBox _txtCron    = new() { Width = 260 };
    private readonly CheckBox _chkEnabled = new() { Text = "Enabled", Checked = true };
    private Button _btnOk   = null!;
    private Button _btnBrowse = null!;
    private readonly SyncSchedule? _existing;

    public ScheduleEditDialog(SyncSchedule? existing)
    {
        _existing = existing;
        BuildUI();
        if (existing is not null) Populate(existing);
    }

    private void BuildUI()
    {
        Text            = _existing is null ? "Add Schedule" : "Edit Schedule";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Size            = new Size(420, 320);
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = MinimizeBox = false;
        BackColor       = Color.FromArgb(45, 45, 48);
        ForeColor       = Color.White;

        var rows = new[]
        {
            ("Name:",       (Control)_txtName),
            ("Local Path:", BuildLocalRow()),
            ("S3 Prefix:",  _txtRemote),
            ("Cron:",       _txtCron),
            ("",            _chkEnabled),
        };

        int y = 14;
        foreach (var (label, ctrl) in rows)
        {
            if (!string.IsNullOrEmpty(label))
            {
                Controls.Add(new Label { Text=label, Location=new Point(12, y+3), AutoSize=true, ForeColor=Color.LightGray });
            }
            ctrl.Location = new Point(120, y);
            Controls.Add(ctrl);
            y += 38;
        }

        // Cron hint
        Controls.Add(new Label
        {
            Text      = "e.g. 0 0 * * * ?  (every hour)\n     0 0 2 * * ?  (daily at 02:00)",
            Location  = new Point(120, y),
            AutoSize  = true,
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 7.5f),
        });

        _btnOk = new Button { Text="OK", DialogResult=DialogResult.OK, Location=new Point(230, 250), Size=new Size(80,28), BackColor=Color.FromArgb(0,122,204), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        var btnCancel = new Button { Text="Cancel", DialogResult=DialogResult.Cancel, Location=new Point(318, 250), Size=new Size(80,28), BackColor=Color.FromArgb(70,70,80), ForeColor=Color.White, FlatStyle=FlatStyle.Flat };
        Controls.AddRange(new Control[] { _btnOk, btnCancel });
        AcceptButton = _btnOk;
        CancelButton = btnCancel;
        _btnOk.Click += OnOk;
    }

    private Panel BuildLocalRow()
    {
        _btnBrowse = new Button { Text="…", Width=28, Height=_txtLocal.Height, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(70,70,80), ForeColor=Color.White };
        _btnBrowse.Click += (s, e) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Select local folder" };
            if (dlg.ShowDialog() == DialogResult.OK) _txtLocal.Text = dlg.SelectedPath;
        };
        var p = new Panel { Width=260, Height=_txtLocal.Height, BackColor=Color.Transparent };
        _txtLocal.Width = 228;
        _btnBrowse.Location = new Point(230, 0);
        p.Controls.AddRange(new Control[] { _txtLocal, _btnBrowse });
        return p;
    }

    private void Populate(SyncSchedule s)
    {
        _txtName.Text      = s.Name;
        _txtLocal.Text     = s.LocalPath;
        _txtRemote.Text    = s.RemotePrefix;
        _txtCron.Text      = s.CronExpression;
        _chkEnabled.Checked = s.IsEnabled;
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))  { MessageBox.Show("Name is required.");  DialogResult = DialogResult.None; return; }
        if (string.IsNullOrWhiteSpace(_txtCron.Text))  { MessageBox.Show("Cron is required.");  DialogResult = DialogResult.None; return; }
        if (string.IsNullOrWhiteSpace(_txtLocal.Text)) { MessageBox.Show("Local path required."); DialogResult = DialogResult.None; return; }

        Result = new SyncSchedule
        {
            Id             = _existing?.Id ?? Guid.NewGuid().ToString("N"),
            Name           = _txtName.Text.Trim(),
            LocalPath      = _txtLocal.Text.Trim(),
            RemotePrefix   = _txtRemote.Text.Trim(),
            CronExpression = _txtCron.Text.Trim(),
            IsEnabled      = _chkEnabled.Checked,
            LastRun        = _existing?.LastRun,
            NextRun        = _existing?.NextRun,
        };
    }
}
