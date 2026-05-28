using FayteWO.Client.Networking;
using FayteWO.Client.Rendering;

const string host = "127.0.0.1";
const int port = 7777;

Console.WriteLine("FayteWO Client Starting...");

string username = PromptForUsername();

GameClient client = new GameClient(host, port);

try
{
    client.Connect();
    client.Login(username, "password");

    Console.WriteLine("Waiting for login response...");

    while (client.PlayerId is null)
    {
        Thread.Sleep(50);
    }

    Console.WriteLine("Login complete.");
    Console.WriteLine("Opening FayteWO visual client.");
    Console.WriteLine("Controls:");
    Console.WriteLine("  WASD / Arrow Keys = move");
    Console.WriteLine("  Left Click        = select tile");
    Console.WriteLine("  Right Click       = select and interact");
    Console.WriteLine("  E                 = interact with selected tile");
    Console.WriteLine("  Escape            = close window");

    using FayteGame game = new FayteGame(client);
    game.Run();
}
finally
{
    client.Disconnect();
}

static string PromptForUsername()
{
    while (true)
    {
        Console.Write("Username: ");
        string? username = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(username))
        {
            return username.Trim();
        }

        Console.WriteLine("Username cannot be empty.");
    }
}