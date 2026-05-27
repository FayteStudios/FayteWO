using System.Net.Sockets;
using System.Text;

namespace FayteWO.Server.Networking;

public sealed class ClientSession
{
    private readonly TcpClient _client;
    private readonly Func<string, string> _packetHandler;

    public ClientSession(TcpClient client, Func<string, string> packetHandler)
    {
        _client = client;
        _packetHandler = packetHandler;
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

                string responseJson = _packetHandler(incomingJson);

                Console.WriteLine($"Sending response: {responseJson}");
                writer.WriteLine(responseJson);
            }
        }
    }
}