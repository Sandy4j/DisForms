using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DisClient
{
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
        private StreamReader? reader;
        private StreamWriter? writer;
        private CancellationTokenSource? cancellationTokenSource;
        
        public bool IsConnected { get; private set; }
        public event Action<string>? MessageReceived;
        public event Action? Disconnected;

        private string serverIP = string.Empty;
        private int serverPort;
        private string username = string.Empty;
        private bool isReconnecting = false;

        public async Task<bool> ConnectAsync(string serverIP, int port, string username)
        {
            try
            {
                this.serverIP = serverIP;
                this.serverPort = port;
                this.username = username;

                tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(serverIP, port);
                stream = tcpClient.GetStream();

                // Setup StreamReader/Writer untuk newline-delimited JSON
                reader = new StreamReader(stream, Encoding.UTF8);
                writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                cancellationTokenSource = new CancellationTokenSource();
                IsConnected = true;

                Console.WriteLine($"[CLIENT] Connected to {serverIP}:{port}");

                _ = Task.Run(ListenForMessagesAsync);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Connection failed: {ex.Message}");
                IsConnected = false;
                return false;
            }
        }

        private async Task ListenForMessagesAsync()
        {
            Console.WriteLine("[CLIENT] Started listening for messages");

            try
            {
                while (IsConnected && tcpClient?.Connected == true && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Read line-by-line (newline-delimited JSON)
                        string? message = await reader.ReadLineAsync();

                        // null berarti stream closed
                        if (message == null)
                        {
                            Console.WriteLine("[CLIENT] Server closed connection (stream null)");
                            break;
                        }

                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(message))
                        {
                            continue;
                        }

                        Console.WriteLine($"[CLIENT] Received: {message}");
                        MessageReceived?.Invoke(message);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"[CLIENT] IO error while reading: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CLIENT] Error reading message: {ex.GetType().Name} - {ex.Message}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Listen loop error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("[CLIENT] Listen loop ended");
                bool wasConnected = IsConnected;
                IsConnected = false;

                // Notify disconnection
                if (wasConnected)
                {
                    Disconnected?.Invoke();
                }

                // Melakukan reconnect jika sebelumnya terhubung
                if (wasConnected && !cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _ = Task.Run(AttemptReconnectAsync);
                }
            }
        }

        private async Task AttemptReconnectAsync()
        {
            if (isReconnecting) return;
            isReconnecting = true;

            Console.WriteLine("[CLIENT] Attempting to reconnect...");

            int retryCount = 0;
            int maxRetries = 5;
            int delayMs = 2000;

            while (retryCount < maxRetries && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                retryCount++;
                Console.WriteLine($"[CLIENT] Reconnect attempt {retryCount}/{maxRetries}");

                await Task.Delay(delayMs);

                try
                {
                    // Cleanup old connection
                    CleanupConnection();

                    // Mencoba untuk reconnect
                    bool success = await ConnectAsync(serverIP, serverPort, username);
                    
                    if (success)
                    {
                        Console.WriteLine("[CLIENT] Reconnected successfully!");
                        
                        // Re-register with server
                        await Task.Delay(300);
                        var registrationPacket = new MessagePackage
                        {
                            type = "register",
                            from = username,
                            package = ""
                        };
                        string json = JsonSerializer.Serialize(registrationPacket);
                        await SendMessageAsync(json);

                        isReconnecting = false;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLIENT] Reconnect failed: {ex.Message}");
                }

                delayMs = Math.Min(delayMs * 2, 10000); // Exponential backoff, max 10s
            }

            Console.WriteLine("[CLIENT] Reconnection attempts exhausted");
            isReconnecting = false;
        }

        public async Task SendMessageAsync(string message)
        {
            if (!IsConnected || tcpClient?.Connected != true || writer == null)
            {
                Console.WriteLine("[CLIENT] Cannot send - not connected");
                return;
            }

            try
            {
                // Kirim dengan newline delimiter
                await writer.WriteLineAsync(message);
                // AutoFlush = true, jadi tidak perlu manual flush
                Console.WriteLine($"[CLIENT] Sent: {message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[CLIENT] Failed to send message: {ex.Message}");
                IsConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Unexpected error sending: {ex.GetType().Name} - {ex.Message}");
                IsConnected = false;
            }
        }

        public void Disconnect()
        {
            Console.WriteLine("[CLIENT] Manual disconnect");
            
            // Cancel reconnect
            cancellationTokenSource?.Cancel();
            
            IsConnected = false;
            CleanupConnection();
        }

        private void CleanupConnection()
        {
            try
            {
                reader?.Dispose();
                writer?.Dispose();
                stream?.Close();
                tcpClient?.Close();
                
                Console.WriteLine("[CLIENT] Connection cleaned up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Error during cleanup: {ex.Message}");
            }
        }
    }
}