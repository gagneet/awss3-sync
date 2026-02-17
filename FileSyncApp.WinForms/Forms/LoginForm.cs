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
        this.Width = 400;
        this.Height = 300;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        var panel = new KryptonPanel { Dock = DockStyle.Fill };

        var lblMode = new KryptonLabel { Text = "Login Mode:", Location = new Point(50, 20) };
        _rbCognito = new KryptonRadioButton { Text = "AWS Cognito", Location = new Point(150, 20), Checked = true };
        _rbLocal = new KryptonRadioButton { Text = "Local Login", Location = new Point(260, 20) };

        var lblUser = new KryptonLabel { Text = "Username:", Location = new Point(50, 60) };
        _txtUsername = new KryptonTextBox { Location = new Point(150, 60), Width = 180 };

        var lblPass = new KryptonLabel { Text = "Password:", Location = new Point(50, 100) };
        _txtPassword = new KryptonTextBox { Location = new Point(150, 100), Width = 180, PasswordChar = '●' };

        _btnLogin = new KryptonButton { Text = "Login", Location = new Point(150, 150), Width = 100 };
        _btnLogin.Click += BtnLogin_Click;

        _lblStatus = new KryptonLabel { Text = "", Location = new Point(50, 210), Width = 300, StateCommon = { ShortText = { Color1 = Color.Red } } };

        var lblNote = new KryptonLabel
        {
            Text = "Local defaults: admin/admin, exec/exec, user/user",
            Location = new Point(50, 240),
            Width = 350,
            StateCommon = { ShortText = { Font = new Font("Segoe UI", 7) } }
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
                _lblStatus.Text = "Invalid username or password.";
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
