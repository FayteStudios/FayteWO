using FayteWO.Client.Networking;
using FayteWO.Shared.World;

const string host = "127.0.0.1";
const int port = 7777;

Console.WriteLine("FayteWO Client Starting...");

GameClient client = new GameClient(host, port);

try
{
    client.Connect();

    bool loggedIn = client.Login("TestPlayer", "password");

    if (!loggedIn)
    {
        Console.WriteLine("Login failed. Client shutting down.");
        return;
    }

    client.SendMoveRequest(Direction.East);
    client.SendMoveRequest(Direction.East);
    client.SendMoveRequest(Direction.East);

    Console.WriteLine("Client test complete.");
}
finally
{
    client.Disconnect();
}