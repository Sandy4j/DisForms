using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

/* Type message
   ["register"] -> gawe message register only
   ["system"] -> gawe message system
   ["chat"] -> gawe chat group
   ["pm"] -> gawe chat private
*/

namespace DisServer
{
    internal class ClientHandler
    {
        private readonly TcpClient client;
        private readonly Server server;
        private readonly NetworkStream stream;
        public string client_id { get; private set; }
        public string? username { get; private set; }
        public string ip { get; private set; }
        public int port { get; private set; }

        public ClientHandler(TcpClient client, Server server)
        {
            var endpoint = client.Client.RemoteEndPoint as System.Net.IPEndPoint;

            this.client = client;
            this.server = server;
            stream = client.GetStream();
            client_id = Guid.NewGuid().ToString();
            ip = endpoint.Address.ToString();
            port = endpoint.Port;
        }

        public async Task HandleClientAsync()
        {
            byte[] buffer = new byte[4096];

            try
            {
                int bytes_read;

                while (true)
                {
                    bytes_read = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytes_read == 0) break;

                    string json_string = Encoding.UTF8.GetString(buffer, 0, bytes_read);

                    var packet = JsonSerializer.Deserialize<MessagePackage>(json_string);

                    switch (packet.type)
                    {
                        case "register":
                            this.username = packet.from;
                            Console.WriteLine($"client {this.client_id} registered as {this.username}");
                            server.BroadcastSystemMessage($"{this.username} join the chat.", this);
                            break;
                        case "system":
                            break;
                        case "chat":
                            if (string.IsNullOrEmpty(this.username)) break;
                            server.BroadcastChatMessage(this.username, packet.package, this);
                            break;
                        case "pm":
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"client error [{username ?? client_id}]: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(username)) server.BroadcastSystemMessage($"{username} left the chat.", this);

                CleanUp();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"failed send message [{username ?? client_id}]: {ex.Message}");
                CleanUp();
            }
        }

        private void CleanUp()
        {
            server.RemoveClient(this);
            client.Close();
        }
    }
}
