using System.Net.Sockets;
using System.Text;

namespace FayteWO.Server.Networking;

public sealed class ClientSession
{
    private readonly TcpClient _client;
    private readonly Func<ClientSession, string, string?> _packetHandler;

  
    private readonly object _sendLock = new();

    private StreamWriter? _writer;

    public Guid SessionId { get; } = Guid.NewGuid();
    public Guid? PlayerId { get; private set; }

    public bool IsLoggedIn => PlayerId is not null;

    public ClientSession(TcpClient client, Func<ClientSession, string, string?> packetHandler)
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
            _writer = writer;

            while (true)
            {
                string? incomingJson = reader.ReadLine();

                if (incomingJson is null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(incomingJson))
                {
                    Console.WriteLine($"Session {SessionId}: Received empty packet.");
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine($"Session {SessionId}: Received raw packet: {incomingJson}");
                
                string? responseJson = _packetHandler(this, incomingJson);

                if (!string.IsNullOrWhiteSpace(responseJson))
                {
                    SendRaw(responseJson);
                }
            }
        }
    }

    public void SendRaw(string json)
    {
        if (_writer is null)
        {
            return;
        }

        lock (_sendLock)
        {
            Console.WriteLine($"Session {SessionId}: Sending packet: {json}");
            _writer.WriteLine(json);
        }
    }
}