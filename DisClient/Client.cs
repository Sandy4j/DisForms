using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DisClient
{
    public class Client
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        public bool IsConnected { get; private set; }
        
        public event Action<string> MessageReceived;

        public async Task<bool> ConnectAsync(string serverIP, int port, string username)
        {
            try
            {
                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIP, port);
                stream = tcpClient.GetStream();
                
                byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
                await stream.WriteAsync(usernameBytes, 0, usernameBytes.Length);
                
                IsConnected = true;
                _ = Task.Run(ListenForMessagesAsync);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
                return false;
            }
        }

        private async Task ListenForMessagesAsync()
        {
            byte[] buffer = new byte[4096];
            
            try
            {
                while (IsConnected && tcpClient.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0) break;
                    
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
            finally
            {
                IsConnected = false;
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected || stream == null) return;
            
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            IsConnected = false;
            stream?.Close();
            tcpClient?.Close();
        }
    }
}
