using System.Drawing;
using System.Windows.Forms;

namespace FayteWO.Client.Login;

public sealed class LoginForm : Form
{
    private readonly TextBox _usernameTextBox;
    private readonly Button _loginButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;

    private string _username = "";

    public LoginForm()
    {
        Text = "FayteWO Login";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 420;
        Height = 320;

        Label titleLabel = new Label
        {
            Text = "FayteWO",
            AutoSize = false,
            Left = 20,
            Top = 20,
            Width = 360,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(FontFamily.GenericSansSerif, 20, FontStyle.Bold)
        };

        Label usernameLabel = new Label
        {
            Text = "Username",
            AutoSize = true,
            Left = 50,
            Top = 80
        };

        _usernameTextBox = new TextBox
        {
            Left = 50,
            Top = 105,
            Width = 300,
            Height = 28,
            MaxLength = 16
        };

        _loginButton = new Button
        {
            Text = "Login",
            Left = 50,
            Top = 150,
            Width = 140,
            Height = 34
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            Left = 210,
            Top = 150,
            Width = 140,
            Height = 34
        };

        _statusLabel = new Label
        {
            Text = "Enter a username to connect.",
            AutoSize = false,
            Left = 50,
            Top = 195,
            Width = 300,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.Add(titleLabel);
        Controls.Add(usernameLabel);
        Controls.Add(_usernameTextBox);
        Controls.Add(_loginButton);
        Controls.Add(_cancelButton);
        Controls.Add(_statusLabel);

        AcceptButton = _loginButton;
        CancelButton = _cancelButton;

        _loginButton.Click += (_, _) => TryAcceptLogin();
        _cancelButton.Click += (_, _) => CancelLogin();

        _usernameTextBox.TextChanged += (_, _) =>
        {
            _statusLabel.Text = "Enter a username to connect.";
        };

        Shown += (_, _) => _usernameTextBox.Focus();
    }

    public string GetUsername()
    {
        return _username;
    }

    private void TryAcceptLogin()
    {
        string username = _usernameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            _statusLabel.Text = "Username cannot be empty.";
            _usernameTextBox.Focus();
            return;
        }

        _username = username;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void CancelLogin()
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}