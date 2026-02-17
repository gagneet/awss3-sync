using FileSyncApp.Core.Interfaces;
using FileSyncApp.Core.Services;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

public partial class LoginForm : KryptonForm
{
    private readonly IAuthService _authService;
    private KryptonTextBox _txtUsername = null!;
    private KryptonTextBox _txtPassword = null!;
    private KryptonButton _btnLogin = null!;
    private KryptonLabel _lblStatus = null!;
    private KryptonRadioButton _rbCognito = null!;
    private KryptonRadioButton _rbLocal = null!;

    public LoginForm(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Login - FileSyncApp";
        this.Width = 550; // Increased width as requested
        this.Height = 350; // Increased height
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        var panel = new KryptonPanel { Dock = DockStyle.Fill };

        var lblMode = new KryptonLabel { Text = "Login Mode:", Location = new Point(50, 30), StateCommon = { ShortText = { Font = new Font("Segoe UI", 10, FontStyle.Bold) } } };
        _rbCognito = new KryptonRadioButton { Text = "AWS Cognito (Secure Online Auth)", Location = new Point(180, 30), Checked = true };
        _rbLocal = new KryptonRadioButton { Text = "Local Login (Offline Fallback)", Location = new Point(180, 60) };

        var lblUser = new KryptonLabel { Text = "Username:", Location = new Point(50, 110) };
        _txtUsername = new KryptonTextBox { Location = new Point(180, 110), Width = 250 };

        var lblPass = new KryptonLabel { Text = "Password:", Location = new Point(50, 150) };
        _txtPassword = new KryptonTextBox { Location = new Point(180, 150), Width = 250, PasswordChar = '●' };

        _btnLogin = new KryptonButton { Text = "Sign In", Location = new Point(180, 200), Width = 120, Height = 40 };
        _btnLogin.Click += BtnLogin_Click;

        _lblStatus = new KryptonLabel { Text = "", Location = new Point(50, 260), Width = 450, StateCommon = { ShortText = { Color1 = Color.Red } } };

        var lblNote = new KryptonLabel
        {
            Text = "Local credentials: admin/admin, exec/exec, user/user",
            Location = new Point(50, 290),
            Width = 450,
            StateCommon = { ShortText = { Font = new Font("Segoe UI", 8, FontStyle.Italic) } }
        };

        panel.Controls.AddRange(new Control[] { lblMode, _rbCognito, _rbLocal, lblUser, _txtUsername, lblPass, _txtPassword, _btnLogin, _lblStatus, lblNote });
        this.Controls.Add(panel);
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        _btnLogin.Enabled = false;
        _lblStatus.Text = "Authenticating...";

        try
        {
            if (_authService is UnifiedAuthService unified)
            {
                unified.CurrentMode = _rbCognito.Checked ? AuthMode.Cognito : AuthMode.Local;
            }

            var user = await _authService.AuthenticateAsync(_txtUsername.Text, _txtPassword.Text);
            if (user != null)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                _lblStatus.Text = "Invalid username or password. Please check your credentials.";
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnLogin.Enabled = true;
        }
    }
}
