using DisServer;
using System.Net;
using System.Net.Sockets;

Console.WriteLine("=== DisServer Chat Server ===");
Console.WriteLine();

string localIP = GetLocalIPAddress();
Console.WriteLine($"[INFO] Local IP detected: {localIP}");

Server server = new Server(localIP, 8080);


try
{
    await server.StartAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL] Server error: {ex.Message}");
    Console.WriteLine($"[FATAL] Stack trace: {ex.StackTrace}");
}

Console.WriteLine("[INFO] Server terminated. Press any key to exit...");
Console.ReadKey();

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
