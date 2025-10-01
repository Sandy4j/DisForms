using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace DisClient;

public partial class ClientLobby : Window
{
    public ClientLobby()
    {
        InitializeComponent();
        LoadUserIP();
        
        ConnectButton.Click += ConnectButton_Click;
        UsernameTextBox.TextChanged += UsernameTextBox_TextChanged;
        UsernameTextBox.KeyDown += UsernameTextBox_KeyDown;
    }

    private void LoadUserIP()
    {
        try
        {
            string localIP = GetLocalIPAddress();
            UserIPTextBox.Text = localIP;
        }
        catch
        {
            UserIPTextBox.Text = "Unable to detect IP";
        }
    }

    private string GetLocalIPAddress()
    {
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(ip.Address))
                        {
                            return ip.Address.ToString();
                        }
                    }
                }
            }
        }
        return "127.0.0.1";
    }

    private void UsernameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            UsernamePlaceholder.Visibility = Visibility.Visible;
        }
        else
        {
            UsernamePlaceholder.Visibility = Visibility.Collapsed;
        }
    }

    private void UsernameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            ConnectButton_Click(sender, new RoutedEventArgs());
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
        {
            ShowStatus("Please enter a username.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerIPTextBox.Text))
        {
            ShowStatus("Please enter server IP.", true);
            return;
        }

        if (!int.TryParse(ServerPortTextBox.Text, out int port) || port <= 0 || port > 65535)
        {
            ShowStatus("Please enter a valid port number (1-65535).", true);
            return;
        }

        ConnectButton.IsEnabled = false;
        LoadingPanel.Visibility = Visibility.Visible;
        ShowStatus("Connecting to server...", false);

        try
        {
            Client testClient = new Client();
            bool connected = await testClient.ConnectAsync(ServerIPTextBox.Text, port, UsernameTextBox.Text.Trim());

            if (!connected)
            {
                ShowStatus("Failed to connect to server.", true);
                testClient?.Disconnect();
                return;
            }

            // Setup handler untuk cek response
            bool? registrationSuccess = null;
            var tcs = new TaskCompletionSource<bool>();

            void OnRegistrationResponse(string message)
            {
                try
                {
                    var packet = System.Text.Json.JsonSerializer.Deserialize<MessagePackage>(message);
                    if (packet?.type == "registration_response")
                    {
                        registrationSuccess = packet.package == "success";
                        tcs.TrySetResult(true);
                    }
                }
                catch { }
            }

            testClient.MessageReceived += OnRegistrationResponse;

            // Send registration message
            var registrationPacket = new MessagePackage
            {
                type = "register",
                from = UsernameTextBox.Text.Trim(),
                package = "",
                timestamp = DateTime.Now
            };
            string json = System.Text.Json.JsonSerializer.Serialize(registrationPacket);
            await testClient.SendMessageAsync(json);

            // Wait for either success or timeout
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(timeoutTask, tcs.Task);

            testClient.MessageReceived -= OnRegistrationResponse;

            if (completedTask == timeoutTask || registrationSuccess != true)
            {
                ShowStatus("Registration failed or timed out", true);
                testClient?.Disconnect();
                return;
            }

            // Success - buka MainWindow
            ShowStatus("Connected successfully!", false);
            await Task.Delay(500);

            MainWindow mainWindow = new MainWindow(
                testClient,
                UsernameTextBox.Text.Trim(),
                ServerIPTextBox.Text,
                port
            );

            mainWindow.Show();
            this.Close();
        }
        catch (Exception ex)
        {
            ShowStatus($"Connection error: {ex.Message}", true);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError ? 
            System.Windows.Media.Brushes.Red : 
            System.Windows.Media.Brushes.Green;
        StatusTextBlock.Visibility = Visibility.Visible;
        
        if (!isError)
        {
            Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    StatusTextBlock.Visibility = Visibility.Collapsed;
                });
            });
        }
    }
}