using DisServer;
using System.Net;
using System.Net.Sockets;

string localIP = GetLocalIPAddress();

Server server = new Server(localIP, 8080);
await server.StartAsync();

static string GetLocalIPAddress()
{
    try
    {
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
    }
    catch
    {
        return "127.0.0.1";
    }
}
