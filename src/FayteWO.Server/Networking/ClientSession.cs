using System.Net.Sockets;
using System.Text;

namespace FayteWO.Server.Networking;

public sealed class ClientSession
{
    private readonly TcpClient _client;
    private readonly Func<ClientSession, string, string> _packetHandler;

    public Guid? PlayerId { get; private set; }

    public bool IsLoggedIn => PlayerId is not null;

    public ClientSession(TcpClient client, Func<ClientSession, string, string> packetHandler)
    {
        _client = client;
        _packetHandler = packetHandler;
    }

    public void SetPlayerId(Guid playerId)
    {
        PlayerId = playerId;
    }

    public void Run()
    {
        using (_client)
        using (NetworkStream stream = _client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
        {
            AutoFlush = true
        })
        {
            while (true)
            {
                string? incomingJson = reader.ReadLine();

                if (incomingJson is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(incomingJson))
                {
                    Console.WriteLine("Received empty packet.");
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"Received raw packet: {incomingJson}");

                string responseJson = _packetHandler(this, incomingJson);

                Console.WriteLine($"Sending response: {responseJson}");
                writer.WriteLine(responseJson);
            }
        }
    }
}