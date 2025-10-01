using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace DisServer
{
    public class ChatLog
    {
        public string client_id { get; set; }
        public string username {  get; set; }
        public string message { get; set; }
        public DateTime time { get; set; }
    }

    public class MessagePackage
    {
        [JsonPropertyName("type")]
        public string type { get; set; }
    
        [JsonPropertyName("to")]
        public string? to { get; set; }
    
        [JsonPropertyName("from")]
        public string from { get; set; }
    
        [JsonPropertyName("package")]
        public string? package { get; set; }
    
        [JsonPropertyName("timestamp")]
        public DateTime? timestamp { get; set; }
    }

    internal class Server
    {
        private readonly TcpListener listener;
        private readonly Dictionary<string, ClientHandler> clients = new Dictionary<string, ClientHandler>();
        private readonly List<ChatLog> messages = new List<ChatLog>();
        private CancellationTokenSource cancellationTokenSource;
        private bool isRunning = false;

        public Server(string ip, int port)
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            cancellationTokenSource = new CancellationTokenSource();
            LoadChatLog();
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"[SERVER] Starting on {((IPEndPoint)listener.LocalEndpoint).Address}:{((IPEndPoint)listener.LocalEndpoint).Port}");

            listener.Start();
            isRunning = true;
            Console.WriteLine("[SERVER] Server is running and listening for connections...");

            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Gunakan AcceptTcpClientAsync dengan cancellation token
                        TcpClient temp_client = await listener.AcceptTcpClientAsync();
                        
                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            temp_client.Close();
                            break;
                        }

                        Console.WriteLine($"[ACCEPT] New client connection from {temp_client.Client.RemoteEndPoint}");

                        ClientHandler temp_client_handler = new ClientHandler(temp_client, this);

                        Console.WriteLine($"[INFO] Total connected clients: {clients.Count}");

                        _ = Task.Run(async () => await temp_client_handler.HandleClientAsync());
                    }
                    catch (ObjectDisposedException)
                    {
                        // Listener sudah di-stop, ini normal saat shutdown
                        Console.WriteLine("[INFO] Listener stopped");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Console.WriteLine($"[ERROR] Error accepting client: {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                Console.WriteLine("[SERVER] Server loop ended");
            }
        }

        public void Stop()
        {
            if (!isRunning) return;

            Console.WriteLine("[SERVER] Stopping server...");
            cancellationTokenSource.Cancel();
            
            // stop listener
            listener.Stop();
            
            // force close untuk semua klien yang terhubung
            List<ClientHandler> clientsList;
            lock (clients)
            {
                clientsList = clients.Values.ToList();
            }
            
            Console.WriteLine($"[SERVER] Closing {clientsList.Count} connected clients...");
            foreach (var client in clientsList)
            {
                try
                {
                    client.ForceClose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error closing client {client.username ?? client.client_id}: {ex.Message}");
                }
            }
            
            isRunning = false;
            Console.WriteLine("[SERVER] Server stopped");
        }
        
        public async Task BroadcastTypingStatus(string username, bool isTyping, ClientHandler? sender = null)
        {
            var package = new MessagePackage
            {
                type = "typing",
                from = username,
                package = isTyping ? "true" : "false"
            };
        
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
        
            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"[BROADCAST] Typing status: {json_package}");
        
            // kirim ke semua klien yang terdaftar kecuali pengirim
            var targetClients = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username) && c != sender)
                .ToList();
        
            Console.WriteLine($"[BROADCAST] Sending typing status to {targetClients.Count} clients");
        
            var tasks = new List<Task>();
            foreach (var client in targetClients)
            {
                tasks.Add(client.SendMessageAsync(json_package));
            }
        
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("[BROADCAST] Typing status completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error broadcasting typing status: {ex.Message}");
            }
        }

        public async Task BroadcastChatMessage(string username, string message, ClientHandler? client = null)
        {
            if (client == null) return;

            var messageTime = DateTime.Now;
            var package = new MessagePackage
            {
                type = "chat",
                from = username,
                to = string.Empty,
                package = message,
                timestamp = messageTime
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"[BROADCAST] Chat message: {json_package}");

            var chat_log = new ChatLog
            {
                client_id = client.client_id,
                username = client.username,
                message = message,
                time = messageTime
            };
            messages.Add(chat_log);

            // kirim ke semua klien yang terdaftar
            var registeredClients = clients.Values.Where(c => !string.IsNullOrEmpty(c.username)).ToList();
            Console.WriteLine($"[BROADCAST] Sending chat to {registeredClients.Count} registered clients");
    
            var tasks = new List<Task>();
            foreach (var item in registeredClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }
    
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("[BROADCAST] Chat message completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error broadcasting chat message: {ex.Message}");
            }

            SaveChatLog();
        }

        public async Task BroadcastPrivateChatMessage(string username, string message, string target, ClientHandler? client = null)
        {
            if (client == null) return;

            var messageTime = DateTime.Now;
            var package = new MessagePackage
            {
                type = "pm",
                from = username,
                to = target,
                package = message, // message udah clean dari client
                timestamp = messageTime
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"[BROADCAST] Private message: {json_package}");

            var chat_log = new ChatLog
            {
                client_id = client.client_id,
                username = client.username,
                message = $"<{target}> {message}", // save dengan format lengkap
                time = messageTime
            };
            messages.Add(chat_log);

            // kirim ke target user dan pengirim
            var targetClients = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username) && 
                            (c.username == username || c.username == target))
                .ToList();

            Console.WriteLine($"[BROADCAST] Sending PM to {targetClients.Count} clients (sender + target)");

            var tasks = new List<Task>();
            foreach (var item in targetClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }

            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("[BROADCAST] Private message completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error broadcasting private message: {ex.Message}");
            }

            SaveChatLog();
        }

        public async Task BroadcastSystemMessage(string message, ClientHandler? client = null)
        {
            Console.WriteLine($"[BROADCAST] System message: {message}");

            var package = new MessagePackage
            {
                type = "system",
                from = "server",
                package = message
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"[BROADCAST] System JSON: {json_package}");

            var targetClients = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username) && c != client)
                .ToList();
            
            Console.WriteLine($"[BROADCAST] Sending system message to {targetClients.Count} clients");
            
            var tasks = new List<Task>();
            foreach (var item in targetClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }
            
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("[BROADCAST] System message completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error broadcasting system message: {ex.Message}");
            }

            if (client != null)
            {
                if (clients.Count > 0)
                {
                    foreach (var item in clients)
                    {
                        if (item.Value.username != client.username) continue;

                        RemoveClient(item.Value);
                        return;
                    }
                }

                lock (clients)
                {
                    clients.Add(client.client_id, client);
                }
            }
        }

        public async Task BroadcastUsersList()
        {
            var usernames = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username))
                .Select(c => c.username)
                .ToList();

            Console.WriteLine($"[BROADCAST] Users list: [{string.Join(", ", usernames)}]");

            var package = new MessagePackage
            {
                type = "users_list",
                from = "server",
                package = System.Text.Json.JsonSerializer.Serialize(usernames)
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"[BROADCAST] Users list JSON: {json_package}");

            // kirim ke semua klien yang terdaftar
            var registeredClients = clients.Values.Where(c => !string.IsNullOrEmpty(c.username)).ToList();
            Console.WriteLine($"[BROADCAST] Sending users list to {registeredClients.Count} clients");
            
            var tasks = new List<Task>();
            foreach (var item in registeredClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }
            
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("[BROADCAST] Users list completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error broadcasting users list: {ex.Message}");
            }
        }

        public bool IsUsernameTaken(string username)
        {
            if (clients.Count < 2) return false;

            lock (clients)
            {
                return clients.Values.Any(c =>
                    !string.IsNullOrEmpty(c.username) &&
                    c.username.Equals(username, StringComparison.OrdinalIgnoreCase));
            }
        }

        public async Task SendRegistrationResponse(ClientHandler client, bool success, string message)
        {
            var package = new MessagePackage
            {
                type = "registration_response",
                from = "server",
                package = message,
                timestamp = DateTime.Now
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            await client.SendMessageAsync(json_package);
        }

        public void RemoveClient(ClientHandler? client = null)
        {
            if (client == null) return;

            Console.WriteLine($"[REMOVE] Removing client {client.username ?? client.client_id}");

            lock (clients)
            {
                clients.Remove(client.client_id);
            }
            
            Console.WriteLine($"[REMOVE] Client {client.username ?? client.client_id} removed. Total clients: {clients.Count}");
            _ = Task.Run(async () => await BroadcastUsersList());
        }

        private void SaveChatLog()
        {
            try
            {
                string file_name = "chat.log.json";
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(messages, options);
                System.IO.File.WriteAllText(file_name, json);
                Console.WriteLine($"[SAVE] Chat log saved: {file_name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error saving chat log: {ex.Message}");
            }
        }

        private void LoadChatLog()
        {
            try
            {
                string file_name = "chat.log.json";

                if (!System.IO.File.Exists(file_name)) 
                {
                    Console.WriteLine("[LOAD] No existing chat log found");
                    return;
                }

                string json = System.IO.File.ReadAllText(file_name);
                var loadedMessages = System.Text.Json.JsonSerializer.Deserialize<List<ChatLog>>(json);

                if (loadedMessages == null) 
                {
                    Console.WriteLine("[LOAD] Chat log file is empty");
                    return;
                }

                messages.AddRange(loadedMessages);
                Console.WriteLine($"[LOAD] Loaded {loadedMessages.Count} messages from: {file_name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error loading chat log: {ex.Message}");
            }
        }
    }
}