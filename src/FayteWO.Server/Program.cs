using FayteWO.Server.Networking;

const int port = 7777;

GameServer server = new GameServer(port);
server.Start();