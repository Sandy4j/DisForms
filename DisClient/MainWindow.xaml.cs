using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Linq;
using System;
using System.Threading.Tasks;

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
            
            // Subscribe to message events
            client.MessageReceived += OnMessageReceived;
            
            this.Title = $"Disclite - {username} @ {serverIP}:{serverPort}";
            SendButton.Click += SendButton_Click;
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;
            MessageTextBox.KeyDown += MessageTextBox_KeyDown;
            
            _ = Task.Run(async () => {
                await Task.Delay(500); // Give connection time to stabilize
                await SendRegistrationMessage();
            });
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

        private void OnMessageReceived(string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnMessageReceived(message));
                return;
            }

            try
            {
                // Try to parse as JSON
                var messagePacket = JsonSerializer.Deserialize<MessagePackage>(message);
        
                switch (messagePacket.type)
                {
                    case "system":
                        AddSystemMessage(messagePacket.package);
                        break;
                
                    case "chat":
                        AddChatMessage(messagePacket.from, messagePacket.package);
                        break;
                        
                    case "users_list":
                        HandleUsersList(messagePacket.package);
                        break;
                        
                    case "user_joined":
                        HandleUserJoined(messagePacket.package);
                        break;
                        
                    case "user_left":
                        HandleUserLeft(messagePacket.package);
                        break;
                
                    default:
                        AddSystemMessage($"Unknown message: {message}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                AddSystemMessage(message);
            }
            catch (Exception ex)
            {
                AddSystemMessage($"Error: {ex.Message}");
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

        private void HandleUsersList(string usersJson)
        {
            var users = JsonSerializer.Deserialize<List<string>>(usersJson);
            onlineUsers = users ?? new List<string>();
            UpdateOnlineUsersList();
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
                        System.Windows.Media.Brushes.Blue : 
                        System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(5, 2, 5, 2)
                };
                
                OnlineUsersListBox.Items.Add(listItem);
                }
        }

        private void AddChatMessage(string sender, string messageContent)
        {
            TextBlock messageBlock = new TextBlock
            {
                Text = $"[{sender}]: {messageContent}",
                Foreground = sender == username ? 
                    System.Windows.Media.Brushes.Blue : 
                    System.Windows.Media.Brushes.Black,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap
            };
            
            ChatPanel.Children.Add(messageBlock);
            ChatScrollViewer.ScrollToEnd();
            
        }

        private void AddSystemMessage(string message)
        {
            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic
            };
            
            ChatPanel.Children.Add(messageBlock);
            ChatScrollViewer.ScrollToEnd();
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
                PlaceholderText.Visibility = Visibility.Visible;
            }
        }

        private void MessageTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SendButton_Click(sender, new RoutedEventArgs());
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
}