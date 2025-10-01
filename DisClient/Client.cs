using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DisClient
{
    /*internal class MessagePackage
    {
        [JsonPropertyName("type")]
        public string type { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string from { get; set; } = string.Empty;

        [JsonPropertyName("package")]
        public string package { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime? timestamp { get; set; }
    }*/

    internal class MessagePackage
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

    public class Client
    {
        private TcpClient? tcpClient;
        private NetworkStream? stream;
        public bool IsConnected { get; private set; }

        public event Action<string>? MessageReceived;

        public async Task<bool> ConnectAsync(string serverIP, int port, string username)
        {
            try
            {
                Console.WriteLine($"Attempting to connect to {serverIP}:{port}");
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIP, port);
                stream = tcpClient.GetStream();
                Console.WriteLine("TCP connection established");

                IsConnected = true;
                _ = Task.Run(ListenForMessagesAsync);

                // Send registration message
                var registrationMessage = new MessagePackage
                {
                    type = "register",
                    from = username,
                    package = "",
                    timestamp = DateTime.Now
                };

                string json = System.Text.Json.JsonSerializer.Serialize(registrationMessage);
                await SendMessageAsync(json);
                Console.WriteLine("Registration message sent");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }

        private async Task ListenForMessagesAsync()
        {
            byte[] buffer = new byte[4096];
            
            while (IsConnected && tcpClient?.Connected == true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                MessageReceived?.Invoke(message);
            }
            
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected || tcpClient?.Connected != true)
            {
                return;
            }

            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(buffer, 0, buffer.Length);
            await stream.FlushAsync();
            
        }

        public void Disconnect()
        {
            IsConnected = false;
            stream?.Close();
            tcpClient?.Close();
        }
    }

}