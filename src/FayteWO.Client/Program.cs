using FayteWO.Client.Networking;
using FayteWO.Shared.World;

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

    PrintHelp();

    while (true)
    {
        Console.WriteLine();
        Console.Write("> ");

        string? input = Console.ReadLine();

        if (input is null)
        {
            continue;
        }

        input = input.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            continue;
        }

        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "move":
            case "m":
                HandleMoveCommand(parts);
                break;

            case "north":
            case "n":
                client.SendMoveRequest(Direction.North);
                break;

            case "east":
            case "e":
                client.SendMoveRequest(Direction.East);
                break;

            case "south":
            case "s":
                client.SendMoveRequest(Direction.South);
                break;

            case "west":
            case "w":
                client.SendMoveRequest(Direction.West);
                break;

            case "pos":
            case "position":
                Console.WriteLine($"Current local position: {client.Position}");
                break;

            case "help":
            case "h":
                PrintHelp();
                break;

            case "quit":
            case "exit":
            case "q":
                Console.WriteLine("Quitting client.");
                return;
            case "entities":
            case "ents":
                client.PrintKnownEntities();
                break;
                case "say":
            case "chat":
                HandleChatCommand(parts);
                break;
                case "whisper":
            case "tell":
            case "wsp":
            case "pm":
                HandleWhisperCommand(parts);
                break;
            case "map":
                client.RequestCurrentChunk();
                break;

            case "localmap":
                client.PrintMapAroundPlayer();
                break;
            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type 'help' for available commands.");
                break;
        }
    }
}
finally
{
    client.Disconnect();
}

string PromptForUsername()
{
    while (true)
    {
        Console.Write("Enter username: ");

        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("Username cannot be empty.");
            continue;
        }

        string username = input.Trim();

        if (username.Length > 20)
        {
            Console.WriteLine("Username cannot be longer than 20 characters.");
            continue;
        }

        return username;
    }
}

void HandleChatCommand(string[] parts)
{
    if (parts.Length < 2)
    {
        Console.WriteLine("Usage: say your message here");
        return;
    }

    string message = string.Join(' ', parts.Skip(1));

    client.SendChatMessage(message);
}

void HandleWhisperCommand(string[] parts)
{
    if (parts.Length < 3)
    {
        Console.WriteLine("Usage: whisper <username> <message>");
        return;
    }

    string targetUsername = parts[1];
    string message = string.Join(' ', parts.Skip(2));

    client.SendWhisperMessage(targetUsername, message);
}

void HandleMoveCommand(string[] parts)
{
    if (parts.Length < 2)
    {
        Console.WriteLine("Usage: move north|east|south|west");
        return;
    }

    if (!TryParseDirection(parts[1], out Direction direction))
    {
        Console.WriteLine($"Unknown direction: {parts[1]}");
        Console.WriteLine("Use north, east, south, or west.");
        return;
    }

    client.SendMoveRequest(direction);
}

bool TryParseDirection(string value, out Direction direction)
{
    switch (value.ToLowerInvariant())
    {
        case "north":
        case "n":
            direction = Direction.North;
            return true;

        case "east":
        case "e":
            direction = Direction.East;
            return true;

        case "south":
        case "s":
            direction = Direction.South;
            return true;

        case "west":
        case "w":
            direction = Direction.West;
            return true;

        default:
            direction = Direction.North;
            return false;
    }
}

void PrintHelp()
{
    Console.WriteLine();
    Console.WriteLine("Available commands:");
    Console.WriteLine("  move north    Move north");
    Console.WriteLine("  move east     Move east");
    Console.WriteLine("  move south    Move south");
    Console.WriteLine("  move west     Move west");
    Console.WriteLine("  n/e/s/w       Shortcut movement commands");
    Console.WriteLine("  pos           Print current local player position");
    Console.WriteLine("  help          Show commands");
    Console.WriteLine("  entities      Print known entities");
    Console.WriteLine("  whisper <user> <message>  Send private message");
    Console.WriteLine("  say <message>  Send chat message");
    Console.WriteLine("  map           Request and print current chunk map");
    Console.WriteLine("  localmap      Print map using already-known chunk data");
    Console.WriteLine("  quit          Exit client");
}