using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Linq;

namespace DisClient
{
    public partial class MainWindow : Window
    {
        private Client client;
        private string username;
        private string serverIP;
        private int serverPort;
        private List<string> onlineUsers = new List<string>();

        public MainWindow(Client connectedClient, string userName, string srvIP, int srvPort)
        {
            InitializeComponent();
            
            client = connectedClient;
            username = userName;
            serverIP = srvIP;
            serverPort = srvPort;
            
            client.MessageReceived += OnMessageReceived;
            
            this.Title = $"Disclite - {username} @ {serverIP}:{serverPort}";
            SendRegistrationMessage();
        }

        public MainWindow()
        {
            InitializeComponent();
            username = "User";
            ConnectToServer();
        }

        private async void SendRegistrationMessage()
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

        private async void ConnectToServer()
        {
            client = new Client();
            client.MessageReceived += OnMessageReceived;
            
            bool connected = await client.ConnectAsync("127.0.0.1", 8888, username);
            
            if (!connected)
            {
                MessageBox.Show("Failed to connect to server!");
            }
            else
            {
                SendRegistrationMessage();
            }
        }

        private void OnMessageReceived(string message)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var messagePacket = JsonSerializer.Deserialize<MessagePackage>(message);
                    
                    switch (messagePacket.type)
                    {
                        case "user_joined":
                            HandleUserJoined(messagePacket.from);
                            AddSystemMessage($"{messagePacket.from} joined the chat");
                            break;
                            
                        case "user_left":
                            HandleUserLeft(messagePacket.from);
                            AddSystemMessage($"{messagePacket.from} left the chat");
                            break;
                            
                        case "users_list":
                            HandleUsersList(messagePacket.package);
                            break;
                            
                        case "chat":
                            AddChatMessage(messagePacket.from, messagePacket.package);
                            break;
                            
                        default:
                            AddSystemMessage(message);
                            break;
                    }
                }
                catch (JsonException)
                {
                    AddSystemMessage(message);
                }
            });
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

        private void HandleUsersList(string usersJson)
        {
            try
            {
                var users = JsonSerializer.Deserialize<List<string>>(usersJson);
                onlineUsers = users ?? new List<string>();
                UpdateOnlineUsersList();
            }
            catch (JsonException)
            {
                if (!string.IsNullOrEmpty(usersJson))
                {
                    onlineUsers = usersJson.Split(',').Select(u => u.Trim()).ToList();
                    UpdateOnlineUsersList();
                }
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
                    Foreground = user == username ? 
                        System.Windows.Media.Brushes.LightGreen : 
                        System.Windows.Media.Brushes.White,
                    Margin = new Thickness(5, 2, 5, 2)
                };
                
                OnlineUsersListBox.Items.Add(listItem);
            }
        }

        private void AddChatMessage(string sender, string messageContent)
        {
            var scrollViewer = FindName("ChatScrollViewer") as ScrollViewer;
            if (scrollViewer?.Content is StackPanel chatPanel)
            {
                TextBlock messageBlock = new TextBlock
                {
                    Text = $"[{sender}]: {messageContent}",
                    Foreground = sender == username ? 
                        System.Windows.Media.Brushes.LightBlue : 
                        System.Windows.Media.Brushes.White,
                    Margin = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap
                };
                
                chatPanel.Children.Add(messageBlock);
                scrollViewer.ScrollToEnd();
            }
        }

        private void AddSystemMessage(string message)
        {
            var scrollViewer = FindName("ChatScrollViewer") as ScrollViewer;
            if (scrollViewer?.Content is StackPanel chatPanel)
            {
                TextBlock messageBlock = new TextBlock
                {
                    Text = message,
                    Foreground = System.Windows.Media.Brushes.Yellow,
                    Margin = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap,
                    FontStyle = FontStyles.Italic
                };
                
                chatPanel.Children.Add(messageBlock);
                scrollViewer.ScrollToEnd();
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(message) && client?.IsConnected == true)
            {
                var chatPacket = new MessagePackage
                {
                    type = "chat",
                    from = username,
                    package = message
                };

                string json = JsonSerializer.Serialize(chatPacket);
                await client.SendMessageAsync(json);
                
                MessageTextBox.Clear();
            }
        }

        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                PlaceholderText.Visibility = Visibility.Visible;
            }
            else
            {
                PlaceholderText.Visibility = Visibility.Collapsed;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            client?.Disconnect();
        }
    }

    public class MessagePackage
    {
        public string type { get; set; }
        public string from { get; set; }
        public string package { get; set; }
    }
}