using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DisClient
{
    public class MessagePackage
    {
        [JsonPropertyName("type")]
        public string type { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string from { get; set; } = string.Empty;

        [JsonPropertyName("package")]
        public string package { get; set; } = string.Empty;

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
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIP, port);
                stream = tcpClient.GetStream();

                IsConnected = true;
                _ = Task.Run(ListenForMessagesAsync);

                return true;
            }
            catch (Exception)
            {
                IsConnected = false;
                return false;
            }
        }

        private async Task ListenForMessagesAsync()
        {
            byte[] buffer = new byte[4096];
            
            //while (true)
            while (IsConnected && tcpClient?.Connected == true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                Console.WriteLine(message);
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