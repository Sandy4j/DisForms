using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace DisServer
{
    public class ChatLog
    {
        public string client_id { get; set; }
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

        public Server(string ip, int port)
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            LoadChatLog();
        }

        public async Task StartAsync()
        {
            Console.WriteLine($"Server starting on {((IPEndPoint)listener.LocalEndpoint).Address}:{((IPEndPoint)listener.LocalEndpoint).Port}");

            listener.Start();
            Console.WriteLine("Server is running and listening for connections...");

            while (true)
            {
                try
                {
                    TcpClient temp_client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"New client connection accepted from {temp_client.Client.RemoteEndPoint}");

                    ClientHandler temp_client_handler = new ClientHandler(temp_client, this);
                    Console.WriteLine($"Total connected clients: {clients.Count}");

                    _ = Task.Run(async () => await temp_client_handler.HandleClientAsync());
                    clients.Add(temp_client_handler.client_id, temp_client_handler);
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }

                foreach (var Items in clients)
                {
                    Console.WriteLine(Items.Value.username);
                }
            }
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
            Console.WriteLine($"Broadcasting typing status: {json_package}");
        
            var targetClients = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username) && c != sender)
                .ToList();
        
            Console.WriteLine($"Sending typing status to {targetClients.Count} clients");
        
            var tasks = new List<Task>();
            foreach (var client in targetClients)
            {
                tasks.Add(client.SendMessageAsync(json_package));
            }
        
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("Typing status broadcast completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting typing status: {ex.Message}");
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
                package = message,
                timestamp = messageTime
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package, options);
            Console.WriteLine($"Broadcasting chat message: {json_package}");

            var chat_log = new ChatLog
            {
                client_id = client.client_id,
                message = message,
                time = messageTime
            };
            messages.Add(chat_log);

            // Send to all registered clients
            var registeredClients = clients.Values.Where(c => !string.IsNullOrEmpty(c.username)).ToList();
            Console.WriteLine($"Sending chat to {registeredClients.Count} registered clients");
    
            var tasks = new List<Task>();
            foreach (var item in registeredClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }
    
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("Chat message broadcast completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting chat message: {ex.Message}");
            }

            SaveChatLog();
        }


        public async Task BroadcastSystemMessage(string message, ClientHandler? client = null)
        {
            Console.WriteLine($"Broadcasting system message: {message}");

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
            Console.WriteLine($"System message JSON: {json_package}");

            var targetClients = clients.Values
                .Where(c => !string.IsNullOrEmpty(c.username) && c != client)
                .ToList();
            
            Console.WriteLine($"Sending system message to {targetClients.Count} clients");
            
            var tasks = new List<Task>();
            foreach (var item in targetClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }

            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("System message broadcast completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting system message: {ex.Message}");
            }
            clients.Add(client.client_id, client);
        }

        public async Task BroadcastUsersList()
        {
            List<string> usernames;
            List<ClientHandler> registeredClients;
    
            lock (clients)
            {
                usernames = clients.Values
                    .Where(c => !string.IsNullOrEmpty(c.username))
                    .Select(c => c.username)
                    .ToList();
        
                registeredClients = clients.Values
                    .Where(c => !string.IsNullOrEmpty(c.username))
                    .ToList();
            }

            Console.WriteLine($"Broadcasting users list: [{string.Join(", ", usernames)}] to {registeredClients.Count} clients");

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
            Console.WriteLine($"Users list JSON: {json_package}");

            var tasks = new List<Task>();
            foreach (var item in registeredClients)
            {
                tasks.Add(item.SendMessageAsync(json_package));
            }
    
            try
            {
                await Task.WhenAll(tasks);
                Console.WriteLine("Users list broadcast completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting users list: {ex.Message}");
            }
        }
        
        public bool IsUsernameTaken(string username)
        {
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

            Console.WriteLine($"Removing client {client.username ?? client.client_id}");

            lock (clients)
            {
                clients.Remove(client.client_id);
            }
            
            Console.WriteLine($"Client {client.username ?? client.client_id} disconnected. Total clients: {clients.Count}");
            
            // Broadcast updated users list
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
                Console.WriteLine($"[INFO] Chat saved to: {file_name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving chat log: {ex.Message}");
            }
        }

        private void LoadChatLog()
        {
            try
            {
                string file_name = "chat.log.json";

                if (!System.IO.File.Exists(file_name)) 
                {
                    Console.WriteLine("[INFO] No existing chat log found");
                    return;
                }

                string json = System.IO.File.ReadAllText(file_name);
                var loadedMessages = System.Text.Json.JsonSerializer.Deserialize<List<ChatLog>>(json);

                if (loadedMessages == null) 
                {
                    Console.WriteLine("[INFO] Chat log file is empty");
                    return;
                }

                messages.AddRange(loadedMessages);
                Console.WriteLine($"[INFO] Loaded {loadedMessages.Count} messages from: {file_name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading chat log: {ex.Message}");
            }
        }
    }
}