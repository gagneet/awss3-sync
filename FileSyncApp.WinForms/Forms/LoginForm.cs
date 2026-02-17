using FileSyncApp.Core.Interfaces;
using Krypton.Toolkit;

namespace FileSyncApp.WinForms.Forms;

public partial class LoginForm : KryptonForm
{
    private readonly IAuthService _authService;
    private KryptonTextBox _txtUsername = null!;
    private KryptonTextBox _txtPassword = null!;
    private KryptonButton _btnLogin = null!;
    private KryptonLabel _lblStatus = null!;

    public LoginForm(IAuthService authService)
    {
        _authService = authService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Login - FileSyncApp";
        this.Width = 400;
        this.Height = 250;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        var panel = new KryptonPanel { Dock = DockStyle.Fill };

        var lblUser = new KryptonLabel { Text = "Username:", Location = new Point(50, 40) };
        _txtUsername = new KryptonTextBox { Location = new Point(150, 40), Width = 180 };

        var lblPass = new KryptonLabel { Text = "Password:", Location = new Point(50, 80) };
        _txtPassword = new KryptonTextBox { Location = new Point(150, 80), Width = 180, PasswordChar = '●' };

        _btnLogin = new KryptonButton { Text = "Login", Location = new Point(150, 130), Width = 100 };
        _btnLogin.Click += BtnLogin_Click;

        _lblStatus = new KryptonLabel { Text = "", Location = new Point(50, 180), Width = 300, StateCommon = { ShortText = { Color1 = Color.Red } } };

        panel.Controls.AddRange(new Control[] { lblUser, _txtUsername, lblPass, _txtPassword, _btnLogin, _lblStatus });
        this.Controls.Add(panel);
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        _btnLogin.Enabled = false;
        _lblStatus.Text = "Authenticating...";

        try
        {
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
