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

        // fully-qualified timer type to avoid ambiguity
        private System.Timers.Timer typingTimer;

        private Client client;
        private string username = string.Empty;
        private string serverIP = string.Empty;
        private int serverPort;
        private List<string> onlineUsers = new List<string>();
        private bool isDarkMode = false;
        private bool isTyping = false;

        // Keep both ctors (designer-safe + the one that accepts args)
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            CommonInit();
        }

        public MainWindow(Client connectedClient, string userName, string srvIP, int srvPort)
            : this()
        {
            // store connection info
            client = connectedClient;
            username = userName ?? string.Empty;
            serverIP = srvIP ?? string.Empty;
            serverPort = srvPort;

            Title = $"Disclite - {username} @ {serverIP}:{serverPort}";

            // avoid double-subscribe
            if (client != null)
            {
                client.MessageReceived -= OnMessageReceived;
                client.MessageReceived += OnMessageReceived;

                // send registration shortly after UI ready
                _ = Task.Run(async () =>
                {
                    await Task.Delay(300);
                    await SendRegistrationMessage();
                });
            }
        }

        private void CommonInit()
        {
            // typing timer
            typingTimer = new System.Timers.Timer(2000);
            typingTimer.Elapsed += TypingTimeout;
            typingTimer.AutoReset = false;

            // ensure handlers attached only once (in case called twice)
            SendButton.Click -= SendButton_Click;
            SendButton.Click += SendButton_Click;

            MessageTextBox.KeyDown -= MessageTextBox_KeyDown;
            MessageTextBox.KeyDown += MessageTextBox_KeyDown;

            MessageTextBox.TextChanged -= MessageTextBox_TextChanged;
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;

            // set initial brushes (light theme)
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
            }
        }

        // Handle incoming messages (this is invoked from client's thread)
        private void OnMessageReceived(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnMessageReceived(message));
                return;
            }

            try
            {
                var packet = JsonSerializer.Deserialize<MessagePackage>(message);
                if (packet == null)
                {
                    // raw system / fallback
                    Messages.Add(new ChatMessage { Username = "System", Text = message, Timestamp = DateTime.Now.ToString("HH:mm") });
                    ChatScrollViewer?.ScrollToEnd();
                    return;
                }

                switch (packet.type)
                {
                    case "system":
                        Messages.Add(new ChatMessage { Username = "System", Text = packet.package ?? string.Empty, Timestamp = DateTime.Now.ToString("HH:mm") });
                        break;

                    case "chat":
                        // When server broadcasts to all clients (including sender),
                        // we avoid local-echo; just show the server message here.
                        Messages.Add(new ChatMessage
                        {
                            Username = packet.from ?? "Unknown",
                            Text = packet.package ?? string.Empty,
                            Timestamp = (packet.timestamp?.ToString() ?? DateTime.Now.ToString("HH:mm"))
                        });

                        // hide typing indicator for that user
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

                    default:
                        Messages.Add(new ChatMessage { Username = "System", Text = $"Unknown message type: {packet.type}", Timestamp = DateTime.Now.ToString("HH:mm") });
                        break;
                }

                ChatScrollViewer?.ScrollToEnd();
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage { Username = "System", Text = $"Parsing error: {ex.Message}", Timestamp = DateTime.Now.ToString("HH:mm") });
            }
        }

        // Send button handler
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        // Enter to send, Shift+Enter to newline
        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        // IMPORTANT: when connected we do NOT locally add a message to avoid double-echo.
        // The server will broadcast it back and OnMessageReceived will display it.
        private async Task SendMessageAsync()
        {
            string text = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            if (client?.IsConnected == true)
            {
                var chatPacket = new MessagePackage
                {
                    type = "chat",
                    from = username,
                    package = text
                };

                string json = JsonSerializer.Serialize(chatPacket);
                await client.SendMessageAsync(json);
            }
            else
            {
                // offline/local mode: show message locally
                Messages.Add(new ChatMessage
                {
                    Username = "Me",
                    Text = text,
                    Timestamp = DateTime.Now.ToString("HH:mm")
                });
            }

            MessageTextBox.Clear();
            ChatScrollViewer?.ScrollToEnd();
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Placeholder visibility handled by XAML DataTrigger; keep typing logic here.
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                TypingIndicator.Visibility = Visibility.Collapsed;
                isTyping = false;
                _ = SendTypingStatus(false);
                return;
            }

            if (!isTyping)
            {
                isTyping = true;
                _ = SendTypingStatus(true);
            }

            typingTimer.Stop();
            typingTimer.Start();
            TypingIndicator.Visibility = Visibility.Visible;
        }

        private void TypingTimeout(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                isTyping = false;
                TypingIndicator.Visibility = Visibility.Collapsed;
            });
            _ = SendTypingStatus(false);
        }

        private async Task SendTypingStatus(bool typing)
        {
            if (client?.IsConnected == true)
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
        }

        private void HandleUsersList(string usersJson)
        {
            try
            {
                var users = JsonSerializer.Deserialize<List<string>>(usersJson) ?? new List<string>();
                onlineUsers = users;
                UpdateOnlineUsersList();
            }
            catch { /* ignore parse errors */ }
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
                    Foreground = user == username ? Brushes.Blue : (isDarkMode ? new SolidColorBrush(Color.FromRgb(220, 221, 222)) : Brushes.Black),
                    Margin = new Thickness(5, 2, 5, 2)
                };

                OnlineUsersListBox.Items.Add(listItem);
            }
        }

        private void HandleTyping(string user, string isTypingStr)
        {
            if (user == username) return; // ignore our own typing

            bool otherTyping = string.Equals(isTypingStr, "true", StringComparison.OrdinalIgnoreCase);
            TypingIndicator.Text = otherTyping ? $"{user} is typing..." : string.Empty;
            TypingIndicator.Visibility = otherTyping ? Visibility.Visible : Visibility.Collapsed;
        }

        // THEME: update dynamic resources so DataTemplate repaints instantly
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
            if (client != null)
            {
                client.MessageReceived -= OnMessageReceived;
                client.Disconnect();
            }

            typingTimer?.Stop();
            typingTimer?.Dispose();
        }

        private void OnlineUsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            client.Disconnect();
            SendButton.IsEnabled = false;
            MessageTextBox.IsEnabled = false;
            TypingIndicator.Text = "Disconnected";
            TypingIndicator.Visibility = Visibility.Visible;
            Close();
        }
    }

    public class ChatMessage
    {
        public string Username { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}
