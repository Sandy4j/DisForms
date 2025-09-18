using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DisServer
{
    internal class ClientHandler
    {
        private readonly TcpClient client;
        private readonly Server server;
        private readonly NetworkStream stream;
        public string client_id { get; private set; }
        public string? username { get; private set; }

        public ClientHandler(TcpClient client, Server server) 
        {
            this.client = client;
            this.server = server;
            stream = client.GetStream();
            client_id = Guid.NewGuid().ToString();
        }

        public async Task HandleClientAsync()
        {
            byte[] buffer = new byte[4096];

            try
            {
                int bytes_read = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytes_read > 0)
                {
                    username = Encoding.UTF8.GetString(buffer, 0, bytes_read);
                    server.BroadcastSystemMessage($"[{username}] joined the chat.", this);
                }

                while (true)
                {
                    bytes_read = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytes_read == 0) break;

                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytes_read);
                    server.BroadcastSystemMessage(receivedMessage, this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"client error [{username ?? client_id}]: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(username)) server.BroadcastSystemMessage($"[{username}] left the chat.", this);

                server.RemoveClient(this);
                client.Close();
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
                server.RemoveClient(this);
                client.Close();
            }
        }
    }
}
