using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DisClient
{
    public partial class MainWindow : Window
    {
        private Client client;
        private string username = "User";

        public MainWindow()
        {
            InitializeComponent();
            ConnectToServer();
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
        }

        private void OnMessageReceived(string message)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                // Add message to chat (you'll need to update your XAML to use a proper chat container)
                // For now, showing in a MessageBox as placeholder
                // You should replace this with proper chat UI updates
            });
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(message) && client?.IsConnected == true)
            {
                await client.SendMessageAsync(message);
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
}
