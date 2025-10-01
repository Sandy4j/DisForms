using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

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
            
            Console.WriteLine($"New client connected: {client_id} from {ip}:{port}");
        }

        public async Task HandleClientAsync()
        {
            byte[] buffer = new byte[4096];
            Console.WriteLine($"Started handling client {client_id}");

            try
            {
                while (client.Connected)
                {
                    int bytes_read = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytes_read == 0) 
                    {
                        Console.WriteLine($"Client {client_id} disconnected (0 bytes)");
                        break;
                    }

                    string json_string = Encoding.UTF8.GetString(buffer, 0, bytes_read);
                    Console.WriteLine($"Received from client {client_id}: {json_string}");

                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var packet = JsonSerializer.Deserialize<MessagePackage>(json_string, options);

                        switch (packet.type.ToLower())
                        {
                            case "register":
                                Console.WriteLine($"Processing registration for username: {packet.from}");
                                
                                if (string.IsNullOrEmpty(packet.from))
                                {
                                    await SendRegistrationResponse(false, "Username cannot be empty");
                                    CleanUp();
                                    return;
                                }

                                if (server.IsUsernameTaken(packet.from))
                                {
                                    await server.SendRegistrationResponse(this, false, "username_taken");
                                    CleanUp();
                                    return;
                                }
                                this.username = packet.from;
                                await server.SendRegistrationResponse(this, true, "success");
                                await server.BroadcastSystemMessage($"{this.username} joined the chat.", this);
                                await server.BroadcastUsersList();
                                break;
                            case "system":
                                Console.WriteLine($"System message from {username ?? client_id}: {packet.package}");
                                break;
                            case "chat":
                                if (string.IsNullOrEmpty(this.username)) 
                                {
                                    Console.WriteLine($"Chat message rejected - user not registered: {client_id}");
                                    break;
                                }
                                Console.WriteLine($"Chat message from {this.username}: {packet.package}");

                                if (packet.from != string.Empty)
                                    await server.BroadcastPrivateChatMessage(this.username, packet.package, this);
                                else
                                    await server.BroadcastChatMessage(this.username, packet.package, this);
                                break;
                            case "typing":
                                if (string.IsNullOrEmpty(this.username))
                                {
                                    Console.WriteLine($"Typing status rejected - user not registered: {client_id}");
                                    break;
                                }
                                
                                bool isTyping = string.Equals(packet.package, "true", StringComparison.OrdinalIgnoreCase);
                                Console.WriteLine($"Typing status from {this.username}: {isTyping}");
                                await server.BroadcastTypingStatus(this.username, isTyping, this);
                                break;
                            /*case "pm":
                                Console.WriteLine("---------------------------------------------------------------");
                                if (string.IsNullOrEmpty(this.username))
                                {
                                    Console.WriteLine($"Chat message rejected - user not registered: {client_id}");
                                    break;
                                }
                                Console.WriteLine($"Private message from {username ?? client_id}: {packet.package}");
                                await server.BroadcastPrivateChatMessage(this.username, packet.package, this);
                                break;*/
                            default:
                                Console.WriteLine($"Unknown message type: {packet.type}");
                                break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"JSON parsing error from client {client_id}: {ex.Message}");
                        Console.WriteLine($"Raw message: {json_string}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error [{username ?? client_id}]: {ex.Message}");
            }
            finally
            {
                CleanUp();
            }
        }

        private async Task SendRegistrationResponse(bool success, string message)
        {
            var response = new MessagePackage
            {
                type = "registration_response",
                from = "server",
                package = message
            };

            string json = JsonSerializer.Serialize(response);
            await SendMessageAsync(json);
        }

        public async Task SendMessageAsync(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(buffer, 0, buffer.Length);
            await stream.FlushAsync();
        }

        private void CleanUp()
        {
            Console.WriteLine($"Cleaning up client {username ?? client_id}");
            server.RemoveClient(this);
            stream?.Close();
            client?.Close();
         
        }

        public void SetUsername(string newUsername)
        {
            username = newUsername;
        }
    }
}