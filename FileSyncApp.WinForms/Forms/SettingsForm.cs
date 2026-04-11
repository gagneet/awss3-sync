using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Models;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

/// <summary>
/// Settings dialog for configuring AWS connection, performance tuning, bucket profiles,
/// and sync behaviour. Changes are persisted back to appsettings.json.
/// </summary>
public partial class SettingsForm : KryptonForm
{
    private readonly IConfigurationService _configService;

    // ── AWS tab ──────────────────────────────────────────────────────────────
    private KryptonTextBox _txtAccessKey  = null!;
    private KryptonTextBox _txtSecretKey  = null!;
    private KryptonTextBox _txtRegion     = null!;
    private KryptonTextBox _txtBucket     = null!;

    // ── Performance tab ──────────────────────────────────────────────────────
    private KryptonNumericUpDown _numMaxUploads    = null!;
    private KryptonNumericUpDown _numMaxDownloads  = null!;
    private KryptonNumericUpDown _numChunkSizeMb   = null!;
    private KryptonNumericUpDown _numBandwidthMbps = null!;
    private KryptonCheckBox      _chkEnableCache   = null!;
    private KryptonNumericUpDown _numCacheMins     = null!;

    // ── Profiles tab ─────────────────────────────────────────────────────────
    private ListBox      _profileList   = null!;
    private KryptonButton _btnAddProfile  = null!;
    private KryptonButton _btnEditProfile = null!;
    private KryptonButton _btnDelProfile  = null!;

    // ── Buttons ──────────────────────────────────────────────────────────────
    private KryptonButton _btnSave   = null!;
    private KryptonButton _btnCancel = null!;

    public SettingsForm(IConfigurationService configService)
    {
        _configService = configService;
        BuildUI();
        LoadValues();
    }

    private void BuildUI()
    {
        Text            = "Settings  –  FileSyncApp";
        Width           = 560;
        Height          = 490;
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildAwsPage());
        tabs.TabPages.Add(BuildPerformancePage());
        tabs.TabPages.Add(BuildProfilesPage());

        // ── Bottom button strip ──────────────────────────────────────────────
        var strip = new Panel { Dock = DockStyle.Bottom, Height = 50 };

        _btnSave   = new KryptonButton { Text = "Save",   Width = 90, Height = 30 };
        _btnCancel = new KryptonButton { Text = "Cancel", Width = 90, Height = 30 };

        _btnSave.Location   = new Point(Width - 214, 10);
        _btnCancel.Location = new Point(Width - 114, 10);

        _btnSave.Click   += OnSave;
        _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        strip.Controls.AddRange(new Control[] { _btnSave, _btnCancel });

        Controls.Add(tabs);
        Controls.Add(strip);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    // ── AWS Connection page ───────────────────────────────────────────────────
    private TabPage BuildAwsPage()
    {
        var page = new TabPage("AWS Connection");

        int y = 20;
        AddLabel(page, "Access Key:", 20, y);
        _txtAccessKey = AddTextBox(page, 155, y, 340); y += 36;

        AddLabel(page, "Secret Key:", 20, y);
        _txtSecretKey = AddTextBox(page, 155, y, 340, password: true); y += 36;

        AddLabel(page, "Region:", 20, y);
        _txtRegion = AddTextBox(page, 155, y, 160); y += 36;

        AddLabel(page, "Bucket Name:", 20, y);
        _txtBucket = AddTextBox(page, 155, y, 340); y += 46;

        page.Controls.Add(new Label
        {
            Text      = "⚠  Credentials are stored in appsettings.json (plain text).\n   Consider environment variables in production.",
            Location  = new Point(20, y),
            Width     = 490,
            Height    = 40,
            Font      = new Font("Segoe UI", 8.25f, FontStyle.Italic),
            ForeColor = Color.FromArgb(200, 150, 0)
        });

        return page;
    }

    // ── Performance page ──────────────────────────────────────────────────────
    private TabPage BuildPerformancePage()
    {
        var page = new TabPage("Performance");

        int y = 20;
        AddLabel(page, "Max concurrent uploads:", 20, y);
        _numMaxUploads = AddNumeric(page, 260, y, 1, 32); y += 36;

        AddLabel(page, "Max concurrent downloads:", 20, y);
        _numMaxDownloads = AddNumeric(page, 260, y, 1, 32); y += 36;

        AddLabel(page, "Upload chunk size (MB):", 20, y);
        _numChunkSizeMb = AddNumeric(page, 260, y, 5, 256); y += 36;

        AddLabel(page, "Bandwidth limit (MB/s, 0=unlimited):", 20, y);
        _numBandwidthMbps = AddNumeric(page, 260, y, 0, 10000, decimals: 1); y += 36;

        _chkEnableCache = new KryptonCheckBox
        {
            Text     = "Enable S3 metadata cache",
            Location = new Point(20, y),
            Width    = 260
        };
        _chkEnableCache.CheckedChanged += (s, e) => _numCacheMins.Enabled = _chkEnableCache.Checked;
        page.Controls.Add(_chkEnableCache); y += 30;

        AddLabel(page, "Cache duration (minutes):", 20, y);
        _numCacheMins = AddNumeric(page, 260, y, 1, 60);

        return page;
    }

    // ── Profiles page ─────────────────────────────────────────────────────────
    private TabPage BuildProfilesPage()
    {
        var page = new TabPage("Bucket Profiles");

        page.Controls.Add(new Label
        {
            Text     = "Manage multiple AWS bucket connections. Switch the active profile from the main toolbar.",
            Location = new Point(12, 12),
            Size     = new Size(500, 34),
            Font     = new Font("Segoe UI", 8.5f)
        });

        _profileList = new ListBox
        {
            Location  = new Point(12, 52),
            Size      = new Size(380, 300),
            Font      = new Font("Segoe UI", 9f)
        };
        page.Controls.Add(_profileList);

        _btnAddProfile  = new KryptonButton { Text = "➕ Add",    Location = new Point(402, 52),  Size = new Size(110, 30) };
        _btnEditProfile = new KryptonButton { Text = "✏ Edit",   Location = new Point(402, 90),  Size = new Size(110, 30) };
        _btnDelProfile  = new KryptonButton { Text = "🗑 Delete", Location = new Point(402, 128), Size = new Size(110, 30) };

        _btnAddProfile.Click  += OnAddProfile;
        _btnEditProfile.Click += OnEditProfile;
        _btnDelProfile.Click  += OnDeleteProfile;

        page.Controls.AddRange(new Control[] { _btnAddProfile, _btnEditProfile, _btnDelProfile });
        return page;
    }

    // ── Load current config values into controls ──────────────────────────────
    private void LoadValues()
    {
        var cfg = _configService.GetConfiguration();

        _txtAccessKey.Text  = cfg.AWS.AccessKey;
        _txtSecretKey.Text  = cfg.AWS.SecretKey;
        _txtRegion.Text     = cfg.AWS.Region;
        _txtBucket.Text     = cfg.AWS.BucketName;

        _numMaxUploads.Value    = Math.Clamp(cfg.Performance.MaxConcurrentUploads, 1, 32);
        _numMaxDownloads.Value  = Math.Clamp(cfg.Performance.MaxConcurrentDownloads, 1, 32);
        _numChunkSizeMb.Value   = Math.Clamp(cfg.Performance.ChunkSizeBytes / (1024 * 1024), 5, 256);
        _numBandwidthMbps.Value = (decimal)Math.Max(cfg.Performance.MaxBytesPerSecond / (1024.0 * 1024.0), 0);
        _chkEnableCache.Checked = cfg.Performance.EnableMetadataCache;
        _numCacheMins.Value     = Math.Clamp(cfg.Performance.MetadataCacheDurationMinutes, 1, 60);
        _numCacheMins.Enabled   = cfg.Performance.EnableMetadataCache;

        RefreshProfileList();
    }

    private void RefreshProfileList()
    {
        _profileList.Items.Clear();
        foreach (var p in _configService.GetProfiles())
            _profileList.Items.Add(p.Name);
    }

    // ── Save handler ──────────────────────────────────────────────────────────
    private void OnSave(object? sender, EventArgs e)
    {
        var cfg = _configService.GetConfiguration();
        cfg.AWS.AccessKey  = _txtAccessKey.Text.Trim();
        cfg.AWS.SecretKey  = _txtSecretKey.Text.Trim();
        cfg.AWS.Region     = _txtRegion.Text.Trim();
        cfg.AWS.BucketName = _txtBucket.Text.Trim();

        cfg.Performance.MaxConcurrentUploads         = (int)_numMaxUploads.Value;
        cfg.Performance.MaxConcurrentDownloads       = (int)_numMaxDownloads.Value;
        cfg.Performance.ChunkSizeBytes               = (long)_numChunkSizeMb.Value * 1024 * 1024;
        cfg.Performance.MaxBytesPerSecond            = (long)(_numBandwidthMbps.Value * 1024 * 1024);
        cfg.Performance.EnableMetadataCache          = _chkEnableCache.Checked;
        cfg.Performance.MetadataCacheDurationMinutes = (int)_numCacheMins.Value;

        try
        {
            _configService.Save();
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save settings:\n{ex.Message}", "Save Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ── Profile CRUD ─────────────────────────────────────────────────────────
    private void OnAddProfile(object? sender, EventArgs e)
    {
        using var dlg = new ProfileEditDialog(null);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Profile != null)
        {
            _configService.SaveProfile(dlg.Profile);
            RefreshProfileList();
        }
    }

    private void OnEditProfile(object? sender, EventArgs e)
    {
        if (_profileList.SelectedItem is not string name) return;
        var existing = _configService.GetProfiles().FirstOrDefault(p => p.Name == name);
        if (existing == null) return;

        using var dlg = new ProfileEditDialog(existing);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Profile != null)
        {
            _configService.SaveProfile(dlg.Profile);
            RefreshProfileList();
        }
    }

    private void OnDeleteProfile(object? sender, EventArgs e)
    {
        if (_profileList.SelectedItem is not string name) return;
        if (MessageBox.Show($"Delete profile '{name}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _configService.DeleteProfile(name);
        RefreshProfileList();
    }

    // ── Control factory helpers ───────────────────────────────────────────────
    private static void AddLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text     = text,
            Location = new Point(x, y + 4),
            Width    = 220,
            AutoSize = false
        });
    }

    private static KryptonTextBox AddTextBox(Control parent, int x, int y, int width, bool password = false)
    {
        var tb = new KryptonTextBox
        {
            Location     = new Point(x, y),
            Width        = width,
            PasswordChar = password ? '●' : '\0'
        };
        parent.Controls.Add(tb);
        return tb;
    }

    private static KryptonNumericUpDown AddNumeric(Control parent, int x, int y,
        decimal min, decimal max, int decimals = 0)
    {
        var n = new KryptonNumericUpDown
        {
            Location      = new Point(x, y),
            Width         = 90,
            Minimum       = min,
            Maximum       = max,
            DecimalPlaces = decimals
        };
        parent.Controls.Add(n);
        return n;
    }
}

// ── Inline profile edit dialog ────────────────────────────────────────────────
internal sealed class ProfileEditDialog : Form
{
    public BucketProfile? Profile { get; private set; }

    private readonly KryptonTextBox _txtName   = new() { Width = 280 };
    private readonly KryptonTextBox _txtKey    = new() { Width = 280 };
    private readonly KryptonTextBox _txtSecret = new() { Width = 280, PasswordChar = '●' };
    private readonly KryptonTextBox _txtRegion = new() { Width = 160 };
    private readonly KryptonTextBox _txtBucket = new() { Width = 280 };

    public ProfileEditDialog(BucketProfile? existing)
    {
        Text            = existing == null ? "Add Profile" : "Edit Profile";
        Width           = 460;
        Height          = 340;
        StartPosition   = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;

        int y = 18;
        Row("Profile Name:", _txtName,   y); y += 36;
        Row("Access Key:",   _txtKey,    y); y += 36;
        Row("Secret Key:",   _txtSecret, y); y += 36;
        Row("Region:",       _txtRegion, y); y += 36;
        Row("Bucket Name:",  _txtBucket, y);

        var btnOk     = new KryptonButton { Text = "OK",     Size = new Size(80, 30), Location = new Point(260, 266) };
        var btnCancel = new KryptonButton { Text = "Cancel", Size = new Size(80, 30), Location = new Point(350, 266) };
        btnOk.Click     += OnOk;
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; };
        Controls.AddRange(new Control[] { btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        if (existing != null)
        {
            _txtName.Text   = existing.Name;
            _txtKey.Text    = existing.AccessKey;
            _txtSecret.Text = existing.SecretKey;
            _txtRegion.Text = existing.Region;
            _txtBucket.Text = existing.BucketName;
        }
    }

    private void Row(string label, KryptonTextBox tb, int y)
    {
        Controls.Add(new Label { Text = label, Location = new Point(12, y + 4), Width = 120, AutoSize = false });
        tb.Location = new Point(140, y);
        Controls.Add(tb);
    }

    private void OnOk(object? sender, EventArgs e)
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Profile Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Profile = new BucketProfile
        {
            Name       = name,
            AccessKey  = _txtKey.Text.Trim(),
            SecretKey  = _txtSecret.Text.Trim(),
            Region     = _txtRegion.Text.Trim(),
            BucketName = _txtBucket.Text.Trim()
        };
        DialogResult = DialogResult.OK;
    }
}
