using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DisClient
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        private System.Timers.Timer typingTimer;

        private Client client;
        private string username = string.Empty;
        private string serverIP = string.Empty;
        private int serverPort;
        private List<string> onlineUsers = new List<string>();
        private bool isDarkMode = false;
        private bool isTyping = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            CommonInit();
        }

        public MainWindow(Client connectedClient, string userName, string srvIP, int srvPort)
            : this()
        {
            client = connectedClient;
            username = userName ?? string.Empty;
            serverIP = srvIP ?? string.Empty;
            serverPort = srvPort;

            Title = $"Disclite - {username} @ {serverIP}:{serverPort}";

            if (client != null)
            {
                // Unsubscribe dulu untuk menghindari multiple subscription
                client.MessageReceived -= OnMessageReceived;
                client.MessageReceived += OnMessageReceived;
                
                client.Disconnected -= OnDisconnected;
                client.Disconnected += OnDisconnected;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(300);
                    await SendRegistrationMessage();
                });
            }
        }

        private void CommonInit()
        {
            typingTimer = new System.Timers.Timer(2000);
            typingTimer.Elapsed += TypingTimeout;
            typingTimer.AutoReset = false;

            SendButton.Click -= SendButton_Click;
            SendButton.Click += SendButton_Click;

            MessageTextBox.KeyDown -= MessageTextBox_KeyDown;
            MessageTextBox.KeyDown += MessageTextBox_KeyDown;

            MessageTextBox.TextChanged -= MessageTextBox_TextChanged;
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;

            ApplyLightTheme();
        }

        private async Task SendRegistrationMessage()
        {
            if (client?.IsConnected == true)
            {
                var registrationPacket = new MessagePackage
                {
                    type = "register",
                    from = username,
                    package = ""
                };

                string json = JsonSerializer.Serialize(registrationPacket);
                await client.SendMessageAsync(json);
                
                Console.WriteLine("[UI] Registration message sent");
            }
        }

        private void OnDisconnected()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(OnDisconnected);
                return;
            }

            Console.WriteLine("[UI] Disconnected from server");
            
            Messages.Add(new ChatMessage 
            { 
                Username = "System", 
                Text = "Disconnected from server. Attempting to reconnect...", 
                Timestamp = DateTime.Now.ToString("HH:mm") 
            });
            
            TypingIndicator.Text = "Reconnecting...";
            TypingIndicator.Foreground = Brushes.Orange;
            TypingIndicator.Visibility = Visibility.Visible;
            
            SendButton.IsEnabled = false;
            MessageTextBox.IsEnabled = false;
            
            ChatScrollViewer?.ScrollToEnd();
        }

        private void OnMessageReceived(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnMessageReceived(message));
                return;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var packet = JsonSerializer.Deserialize<MessagePackage>(message, options);
                
                if (packet == null)
                {
                    Messages.Add(new ChatMessage 
                    { 
                        Username = "System", 
                        Text = message, 
                        Timestamp = DateTime.Now.ToString("HH:mm") 
                    });
                    ChatScrollViewer?.ScrollToEnd();
                    return;
                }

                switch (packet.type)
                {
                    case "system":
                        Messages.Add(new ChatMessage 
                        { 
                            Username = "System", 
                            Text = packet.package ?? string.Empty, 
                            Timestamp = DateTime.Now.ToString("HH:mm") 
                        });
                        
                        // cek apakah ini pesan "joined the chat" untuk user kita
                        if (packet.package?.Contains("joined the chat") == true && packet.package.Contains(username))
                        {
                            SendButton.IsEnabled = true;
                            MessageTextBox.IsEnabled = true;
                            TypingIndicator.Text = string.Empty;
                            TypingIndicator.Visibility = Visibility.Collapsed;
                            
                            Messages.Add(new ChatMessage 
                            { 
                                Username = "System", 
                                Text = "Reconnected successfully!", 
                                Timestamp = DateTime.Now.ToString("HH:mm") 
                            });
                        }
                        break;

                    case "chat":
                        Messages.Add(new ChatMessage
                        {
                            Username = packet.from ?? "Unknown",
                            Text = packet.package ?? string.Empty,
                            Timestamp = packet.timestamp?.ToString("HH:mm") ?? DateTime.Now.ToString("HH:mm")
                        });

                        if (packet.from == username)
                        {
                            TypingIndicator.Text = string.Empty;
                            TypingIndicator.Visibility = Visibility.Collapsed;
                        }
                        break;

                    case "users_list":
                        HandleUsersList(packet.package);
                        break;

                    case "user_joined":
                        HandleUserJoined(packet.package);
                        break;

                    case "user_left":
                        HandleUserLeft(packet.package);
                        break;

                    case "typing":
                        HandleTyping(packet.from, packet.package);
                        break;

                    case "pm":
                        if (packet.to != username && packet.from != username) return;

                        Messages.Add(new ChatMessage
                        {
                            Username = $"[PM] {packet.from}",
                            Text = packet.package ?? string.Empty,
                            Timestamp = packet.timestamp?.ToString("HH:mm") ?? DateTime.Now.ToString("HH:mm")
                        });
                        break;

                    default:
                        Console.WriteLine($"[UI] Unknown message type: {packet.type}");
                        Messages.Add(new ChatMessage 
                        { 
                            Username = "System", 
                            Text = $"Unknown message type: {packet.type}", 
                            Timestamp = DateTime.Now.ToString("HH:mm") 
                        });
                        break;
                }

                ChatScrollViewer?.ScrollToEnd();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[UI] JSON parsing error: {ex.Message}");
                Messages.Add(new ChatMessage 
                { 
                    Username = "System", 
                    Text = $"Message parsing error", 
                    Timestamp = DateTime.Now.ToString("HH:mm") 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UI] Error processing message: {ex.Message}");
                Messages.Add(new ChatMessage 
                { 
                    Username = "System", 
                    Text = $"Error: {ex.Message}", 
                    Timestamp = DateTime.Now.ToString("HH:mm") 
                });
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            string text = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;
        
            if (client?.IsConnected != true)
            {
                Messages.Add(new ChatMessage
                {
                    Username = "System",
                    Text = "Cannot send message - not connected to server",
                    Timestamp = DateTime.Now.ToString("HH:mm")
                });
                ChatScrollViewer?.ScrollToEnd();
                return;
            }
        
            try
            {
                var chatPacket = new MessagePackage
                {
                    type = "chat",
                    from = username,
                    package = text
                };
        
                if (text.StartsWith("<") && text.Contains(">"))
                {
                    int start = text.IndexOf('<');
                    int end = text.IndexOf('>');
                    if (start == 0 && end > start)
                    {
                        string targetUser = text.Substring(1, end - 1);
                        chatPacket.to = targetUser; // set target user
                        chatPacket.package = text.Substring(end + 1).Trim(); // message without the <username>
                    }
                }
        
                string json = JsonSerializer.Serialize(chatPacket);
                await client.SendMessageAsync(json);
        
                Console.WriteLine($"[UI] Message sent: {text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UI] Error sending message: {ex.Message}");
                Messages.Add(new ChatMessage
                {
                    Username = "System",
                    Text = $"Failed to send message: {ex.Message}",
                    Timestamp = DateTime.Now.ToString("HH:mm")
                });
            }
        
            MessageTextBox.Clear();
            ChatScrollViewer?.ScrollToEnd();
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                if (isTyping)
                {
                    isTyping = false;
                    _ = SendTypingStatus(false);
                }
                return;
            }

            if (!isTyping)
            {
                isTyping = true;
                _ = SendTypingStatus(true);
            }

            typingTimer.Stop();
            typingTimer.Start();
        }

        private void TypingTimeout(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (isTyping)
                {
                    isTyping = false;
                    _ = SendTypingStatus(false);
                }
            });
        }

        private async Task SendTypingStatus(bool typing)
        {
            if (client?.IsConnected == true)
            {
                try
                {
                    var typingPacket = new MessagePackage
                    {
                        type = "typing",
                        from = username,
                        package = typing ? "true" : "false"
                    };

                    string json = JsonSerializer.Serialize(typingPacket);
                    await client.SendMessageAsync(json);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UI] Error sending typing status: {ex.Message}");
                }
            }
        }

        private void HandleUsersList(string usersJson)
        {
            try
            {
                var users = JsonSerializer.Deserialize<List<string>>(usersJson) ?? new List<string>();
                onlineUsers = users;
                UpdateOnlineUsersList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UI] Error parsing users list: {ex.Message}");
            }
        }

        private void HandleUserJoined(string user)
        {
            if (!onlineUsers.Contains(user))
            {
                onlineUsers.Add(user);
                UpdateOnlineUsersList();
            }
        }

        private void HandleUserLeft(string user)
        {
            if (onlineUsers.Remove(user))
            {
                UpdateOnlineUsersList();
            }
        }

        private void UpdateOnlineUsersList()
        {
            OnlineUsersListBox.Items.Clear();

            foreach (string user in onlineUsers.OrderBy(u => u))
            {
                var listItem = new ListBoxItem
                {
                    Content = user,
                    Foreground = user == username 
                        ? Brushes.Blue 
                        : (isDarkMode 
                            ? new SolidColorBrush(Color.FromRgb(220, 221, 222)) 
                            : Brushes.Black),
                    Margin = new Thickness(5, 2, 5, 2)
                };

                OnlineUsersListBox.Items.Add(listItem);
            }
        }

        private void HandleTyping(string user, string isTypingStr)
        {
            if (user == username) return;

            bool otherTyping = string.Equals(isTypingStr, "true", StringComparison.OrdinalIgnoreCase);
            
            if (otherTyping)
            {
                TypingIndicator.Text = $"{user} is typing...";
                TypingIndicator.Foreground = isDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(114, 118, 125)) 
                    : new SolidColorBrush(Color.FromRgb(119, 119, 119));
                TypingIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                TypingIndicator.Text = string.Empty;
                TypingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkMode = !isDarkMode;
            if (isDarkMode) ApplyDarkTheme();
            else ApplyLightTheme();
        }

        private void ApplyDarkTheme()
        {
            Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            Resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(47, 49, 54));
            Resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            Resources["UsernameBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["TimestampBrush"] = new SolidColorBrush(Color.FromRgb(114, 118, 125));
            Resources["MessageBrush"] = new SolidColorBrush(Color.FromRgb(220, 221, 222));
            Resources["PlaceholderBrush"] = new SolidColorBrush(Color.FromRgb(114, 118, 125));
            Resources["OnlineBarBrush"] = new SolidColorBrush(Color.FromRgb(52, 54, 59));
            Resources["TypeBrush"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            Resources["ChatBrush"] = new SolidColorBrush(Color.FromRgb(49, 49, 52));
        }

        private void ApplyLightTheme()
        {
            Resources["WindowBackgroundBrush"] = new SolidColorBrush(Colors.White);
            Resources["SidebarBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(246, 246, 246));
            Resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            Resources["UsernameBrush"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            Resources["TimestampBrush"] = new SolidColorBrush(Color.FromRgb(119, 119, 119));
            Resources["MessageBrush"] = new SolidColorBrush(Color.FromRgb(17, 17, 17));
            Resources["PlaceholderBrush"] = new SolidColorBrush(Color.FromRgb(138, 138, 138));
            Resources["OnlineBarBrush"] = new SolidColorBrush(Color.FromRgb(212, 212, 212));
            Resources["TypeBrush"] = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            Resources["ChatBrush"] = new SolidColorBrush(Color.FromRgb(239, 239, 238));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("[UI] Window closing");
            
            if (client != null)
            {
                client.MessageReceived -= OnMessageReceived;
                client.Disconnected -= OnDisconnected;
                client.Disconnect();
            }

            typingTimer?.Stop();
            typingTimer?.Dispose();
        }

        private void OnlineUsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OnlineUsersListBox.SelectedItem is ListBoxItem selectedItem && 
                selectedItem.Content is string selectedUsername)
            {
                // cek kalau user klik dirinya sendiri
                if (selectedUsername == username)
                {
                    OnlineUsersListBox.SelectedItem = null;
                    return;
                }
        
                // Setup PM prefix
                string pmPrefix = $"<{selectedUsername}> ";
                MessageTextBox.Text = pmPrefix;
                MessageTextBox.CaretIndex = MessageTextBox.Text.Length;
                MessageTextBox.Focus();
        
                // clear selection
                OnlineUsersListBox.SelectedItem = null;
                
                Console.WriteLine($"[UI] PM mode activated for user: {selectedUsername}");
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[UI] Manual disconnect requested");
            
            if (client != null)
            {
                client.Disconnect();
            }
            
            SendButton.IsEnabled = false;
            MessageTextBox.IsEnabled = false;
            TypingIndicator.Text = "Disconnected";
            TypingIndicator.Visibility = Visibility.Visible;
            
            Messages.Add(new ChatMessage 
            { 
                Username = "System", 
                Text = "You have disconnected from the server", 
                Timestamp = DateTime.Now.ToString("HH:mm") 
            });
            
            ChatScrollViewer?.ScrollToEnd();
        }
    }

    public class ChatMessage
    {
        public string Username { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}