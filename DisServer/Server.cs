using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DisServer
{
    public class ChatLog
    {
        public string client_id;
        public string message;
        public DateTime time;
    }

    public class MessagePackage
    {
        public string type;
        public string? to;
        public string from;
        public string? package;
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
            Console.WriteLine("server runing");
            
            listener.Start();

            while (true)
            {
                TcpClient temp_client = await listener.AcceptTcpClientAsync();

                ClientHandler temp_client_handler = new ClientHandler(temp_client, this);
                clients.Add(temp_client_handler.client_id, temp_client_handler);

                Task.Run(() => temp_client_handler.HandleClientAsync());
            }
        }

        public void BroadcastChatMessage(string username, string  message, ClientHandler? client = null)
        {
            if (client == null) return;

            var package = new MessagePackage
            {
                type = "chat",
                from = username,
                package = message
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package);
            Console.WriteLine($"broadcast chat: {json_package}");

            var chat_log = new ChatLog
            {
                client_id = client.client_id,
                message = message,
                time = DateTime.Now
            };
            messages.Add(chat_log);

            foreach (var item in clients.Values)
            {
                item.SendMessageAsync(json_package);
            }

            SaveChatLog();
        }

        public void BroadcastSystemMessage(string message, ClientHandler? client = null)
        {
            var package = new MessagePackage
            {
                type = "system",
                from = "server",
                package = message
            };

            string json_package = System.Text.Json.JsonSerializer.Serialize(package);
            Console.WriteLine($"broadcast system: {json_package}");

            foreach (var item in clients.Values)
            {
                if (item == client) continue;

                item.SendMessageAsync(json_package);
            }
        }

        public void RemoveClient(ClientHandler? client = null)
        {
            if (client == null) return;

            clients.Remove(client.client_id);
            Console.WriteLine($"Client {client.username ?? client.client_id} disconnected. Total clients: {clients.Count}");
        }

        private void SaveChatLog()
        {
            string file_name = "chat.log.json";
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string json = System.Text.Json.JsonSerializer.Serialize(messages, options);
            System.IO.File.WriteAllText(file_name, json);
            Console.WriteLine($"[INFO] chat saved to :{file_name}");
        }

        private void LoadChatLog()
        {
            string file_name = "chat.log.json";

            if (!System.IO.File.Exists(file_name)) return;

            string json = System.IO.File.ReadAllText(file_name);

            var loadedMessages = System.Text.Json.JsonSerializer.Deserialize<List<ChatLog>>(json);

            if (loadedMessages == null) return;

            messages.AddRange(loadedMessages);
            Console.WriteLine($"[INFO] loaded {loadedMessages.Count} messages from :{file_name}");
        }
    }
}
