using System.Windows.Forms;
using FayteWO.Client.Login;
using FayteWO.Client.Networking;
using FayteWO.Client.Rendering;

namespace FayteWO.Client;

internal static class Program
{
    private const string Host = "127.0.0.1";
    private const int Port = 7777;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using LoginForm loginForm = new LoginForm();

        DialogResult loginResult = loginForm.ShowDialog();

        if (loginResult != DialogResult.OK)
        {
            return;
        }

        GameClient client = new GameClient(Host, Port);

        try
        {
            client.Connect();
            client.Login(loginForm.GetUsername(), "password");

            WaitForLogin(client);

            using FayteGame game = new FayteGame(client);
            game.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to start FayteWO client.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "FayteWO Client Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            client.Disconnect();
        }
    }

    private static void WaitForLogin(GameClient client)
    {
        DateTime timeoutAt = DateTime.UtcNow.AddSeconds(5);

        while (client.PlayerId is null)
        {
            Application.DoEvents();
            Thread.Sleep(25);

            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("Login timed out. Make sure the server is running.");
            }
        }
    }
}