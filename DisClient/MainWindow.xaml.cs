using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DisClient
{
    public partial class MainWindow : Window
    {
        private Client client;
        private string username;
        private string serverIP;
        private int serverPort;
        private List<string> onlineUsers = new List<string>();
        private bool isDarkMode = false;

        public MainWindow(Client connectedClient, string userName, string srvIP, int srvPort)
        {
            InitializeComponent();
            
            client = connectedClient;
            username = userName;
            serverIP = srvIP;
            serverPort = srvPort;
            
            client.MessageReceived += OnMessageReceived;
            
            this.Title = $"Disclite - {username} @ {serverIP}:{serverPort}";
            SendButton.Click += SendButton_Click;
            MessageTextBox.TextChanged += MessageTextBox_TextChanged;
            MessageTextBox.KeyDown += MessageTextBox_KeyDown;
            ThemeToggleButton.Click += ThemeToggleButton_Click;
            
            _ = Task.Run(async () => {
                await Task.Delay(500);
                await SendRegistrationMessage();
            });
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            isDarkMode = !isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                ApplyDarkTheme();
                ThemeToggleButton.Content = "☀️";
            }
            else
            {
                ApplyLightTheme();
                ThemeToggleButton.Content = "🌙";
            }
            RefreshChatMessages();
            UpdateOnlineUsersList();
        }

        private void ApplyDarkTheme()
        {
            this.Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            
            LeftSidebarBorder.Background = new SolidColorBrush(Color.FromRgb(47, 49, 54));
            LeftSidebarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            OnlineUsersLabel.Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222));
            OnlineUsersListBox.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
            OnlineUsersListBox.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            OnlineUsersListBox.Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222));
            
            ChatBorder.Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            TopBarBorder.Background = new SolidColorBrush(Color.FromRgb(47, 49, 54));
            TopBarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            ChatTitleLabel.Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222));
            
            InputAreaBorder.Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            InputAreaBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            MessageTextBox.Background = new SolidColorBrush(Color.FromRgb(64, 68, 75));
            MessageTextBox.Foreground = new SolidColorBrush(Color.FromRgb(220, 221, 222));
            MessageTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            PlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(114, 118, 125));
            
            ChatMessagesBorder.Background = new SolidColorBrush(Color.FromRgb(54, 57, 63));
            ChatMessagesBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
            
            ThemeToggleButton.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 34, 37));
        }

        private void ApplyLightTheme()
        {
            this.Background = new SolidColorBrush(Colors.White);
            
            LeftSidebarBorder.Background = new SolidColorBrush(Color.FromRgb(246, 246, 246));
            LeftSidebarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            OnlineUsersLabel.Foreground = new SolidColorBrush(Color.FromRgb(44, 47, 51));
            OnlineUsersListBox.Background = new SolidColorBrush(Colors.White);
            OnlineUsersListBox.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            OnlineUsersListBox.Foreground = new SolidColorBrush(Color.FromRgb(44, 47, 51));
            
            ChatBorder.Background = new SolidColorBrush(Colors.White);
            TopBarBorder.Background = new SolidColorBrush(Color.FromRgb(246, 246, 246));
            TopBarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            ChatTitleLabel.Foreground = new SolidColorBrush(Color.FromRgb(44, 47, 51));
            
            InputAreaBorder.Background = new SolidColorBrush(Colors.White);
            InputAreaBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            MessageTextBox.Background = new SolidColorBrush(Color.FromRgb(246, 246, 246));
            MessageTextBox.Foreground = new SolidColorBrush(Color.FromRgb(44, 47, 51));
            MessageTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            PlaceholderText.Foreground = new SolidColorBrush(Color.FromRgb(138, 138, 138));
            
            ChatMessagesBorder.Background = new SolidColorBrush(Colors.White);
            ChatMessagesBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            
            ThemeToggleButton.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
        }

        private void RefreshChatMessages()
        {
            foreach (UIElement element in ChatPanel.Children)
            {
                if (element is TextBlock textBlock)
                {
                    if (textBlock.FontStyle == FontStyles.Italic)
                    {
                        textBlock.Foreground = isDarkMode ? 
                            new SolidColorBrush(Color.FromRgb(114, 118, 125)) : 
                            Brushes.Gray;
                    }
                    else if (textBlock.Text.StartsWith($"[{username}]:"))
                    {
                        textBlock.Foreground = Brushes.Blue;
                    }
                    else
                    {
                        textBlock.Foreground = isDarkMode ? 
                            new SolidColorBrush(Color.FromRgb(220, 221, 222)) : 
                            Brushes.Black;
                    }
                }
            }
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
                var messagePacket = JsonSerializer.Deserialize<MessagePackage>(message);
        
                switch (messagePacket.type)
                {
                    case "system":
                        AddSystemMessage(messagePacket.package);
                        break;
                
                    case "chat":
                        AddChatMessage(messagePacket.from, messagePacket.package, messagePacket.timestamp);
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
            catch (JsonException)
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
                        Brushes.Blue : 
                        (isDarkMode ? new SolidColorBrush(Color.FromRgb(220, 221, 222)) : Brushes.Black),
                    Margin = new Thickness(5, 2, 5, 2)
                };
                
                OnlineUsersListBox.Items.Add(listItem);
            }
        }

        private void AddChatMessage(string sender, string messageContent, DateTime? serverTimestamp = null)
        {
            var messagePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            var messageBlock = new TextBlock
            {
                Text = $"[{sender}]: {messageContent}",
                Foreground = sender == username ?
                    Brushes.Blue :
                    (isDarkMode ? new SolidColorBrush(Color.FromRgb(220, 221, 222)) : Brushes.Black),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };

            var timeToShow = serverTimestamp ?? DateTime.Now;
            var timeBlock = new TextBlock
            {
                Text = timeToShow.ToString("HH:mm"),
                Foreground = isDarkMode ?
                    new SolidColorBrush(Color.FromRgb(114, 118, 125)) :
                    Brushes.Gray,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            messagePanel.Children.Add(messageBlock);
            messagePanel.Children.Add(timeBlock);

            ChatPanel.Children.Add(messagePanel);
            ChatScrollViewer.ScrollToEnd();
        }



        private void AddSystemMessage(string message)
        {
            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                Foreground = isDarkMode ? 
                    new SolidColorBrush(Color.FromRgb(114, 118, 125)) : 
                    Brushes.Gray,
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
