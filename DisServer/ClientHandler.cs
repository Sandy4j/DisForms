using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace DisServer
{
    internal class ClientHandler
    {
        private readonly TcpClient client;
        private readonly Server server;
        private readonly NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
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
            
            // Setup StreamReader/Writer untuk newline-delimited JSON
            reader = new StreamReader(stream, Encoding.UTF8);
            writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            
            client_id = Guid.NewGuid().ToString();
            ip = endpoint.Address.ToString();
            port = endpoint.Port;
            
            Console.WriteLine($"[CONNECT] New client: {client_id} from {ip}:{port}");
        }

        public async Task HandleClientAsync()
        {
            Console.WriteLine($"[START] Started handling client {client_id}");

            try
            {
                while (client.Connected)
                {
                    // Read line-by-line (newline-delimited JSON)
                    string? json_string = await reader.ReadLineAsync();

                    // null berarti stream closed
                    if (json_string == null) 
                    {
                        Console.WriteLine($"[DISCONNECT] Client {username ?? client_id} disconnected (stream closed)");
                        break;
                    }

                    // Skip kotak kosong
                    if (string.IsNullOrWhiteSpace(json_string))
                    {
                        continue;
                    }

                    Console.WriteLine($"[RECEIVE] From {username ?? client_id}: {json_string}");

                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var packet = JsonSerializer.Deserialize<MessagePackage>(json_string, options);

                        if (packet == null)
                        {
                            Console.WriteLine($"[ERROR] Failed to deserialize packet from {username ?? client_id}");
                            continue;
                        }

                        switch (packet.type)
                        {
                            case "register":
                                this.username = packet.from;
                                Console.WriteLine($"[REGISTER] Client {this.client_id} registered as '{this.username}'");
                                
                                await server.BroadcastSystemMessage($"{this.username} joined the chat.", this);
                                await server.BroadcastUsersList();
                                break;

                            case "system":
                                Console.WriteLine($"[SYSTEM] From {username ?? client_id}: {packet.package}");
                                break;

                            case "chat":
                                if (string.IsNullOrEmpty(this.username)) 
                                {
                                    Console.WriteLine($"[REJECT] Chat message rejected - user not registered: {client_id}");
                                    break;
                                }
                                Console.WriteLine($"[CHAT] From {this.username}: {packet.package}");

                                if (!string.IsNullOrEmpty(packet.to))
                                    await server.BroadcastPrivateChatMessage(this.username, packet.package, this);
                                else
                                    await server.BroadcastChatMessage(this.username, packet.package, this);
                                break;

                            case "typing":
                                if (string.IsNullOrEmpty(this.username))
                                {
                                    Console.WriteLine($"[REJECT] Typing status rejected - user not registered: {client_id}");
                                    break;
                                }
                                
                                bool isTyping = string.Equals(packet.package, "true", StringComparison.OrdinalIgnoreCase);
                                Console.WriteLine($"[TYPING] From {this.username}: {isTyping}");
                                await server.BroadcastTypingStatus(this.username, isTyping, this);
                                break;

                            default:
                                Console.WriteLine($"[WARN] Unknown message type '{packet.type}' from {username ?? client_id}");
                                break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[ERROR] JSON parsing error from {username ?? client_id}: {ex.Message}");
                        Console.WriteLine($"[ERROR] Raw message: {json_string}");
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[ERROR] IO error for {username ?? client_id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Client error [{username ?? client_id}]: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(username)) 
                {
                    await server.BroadcastSystemMessage($"{username} left the chat.", this);
                }

                CleanUp();
            }
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                // Kirim message dengan newline delimiter
                await writer.WriteLineAsync(message);
                // AutoFlush = true, jadi tidak perlu manual flush
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[ERROR] Failed to send message to {username ?? client_id}: {ex.Message}");
                // Jangan throw, biar tidak mematikan caller thread
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error sending to {username ?? client_id}: {ex.GetType().Name} - {ex.Message}");
            }
        }

        // Method untuk force close dari server (saat shutdown)
        public void ForceClose()
        {
            Console.WriteLine($"[FORCE_CLOSE] Forcing close for {username ?? client_id}");
            CleanUp();
        }

        private void CleanUp()
        {
            try
            {
                Console.WriteLine($"[CLEANUP] Cleaning up {username ?? client_id}");
                
                server.RemoveClient(this);
                
                reader?.Dispose();
                writer?.Dispose();
                stream?.Close();
                client?.Close();
                
                Console.WriteLine($"[CLEANUP] Cleanup completed for {username ?? client_id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error during cleanup for {username ?? client_id}: {ex.Message}");
            }
        }
    }
}