using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DisServer
{
    internal class Server
    {
        private readonly TcpListener listener;
        private readonly List<ClientHandler> clients = new List<ClientHandler>();

        public Server(string ip, int port)
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
        }

        public async Task StartAsync()
        {
            Console.WriteLine("server runing");
            
            listener.Start();

            while (true)
            {
                TcpClient temp_client = await listener.AcceptTcpClientAsync();

                ClientHandler temp_client_handler = new ClientHandler(temp_client, this);
                clients.Add(temp_client_handler);

                Task.Run(() => temp_client_handler.HandleClientAsync());
            }
        }

        public void BroadcastChatMessage(string username, string  message, ClientHandler client)
        {
            string format_message = $"[{username}]: {message}";
            Console.WriteLine($"broadcast chat: {format_message}");

            foreach (var item in clients)
            {
                if (item == client) continue;

                item.SendMessageAsync(format_message);
            }
        }

        public void BroadcastSystemMessage(string message, ClientHandler? client = null)
        {
            string format_message = $"[SYSTEM]: {message}";
            Console.WriteLine($"broadcast system: {format_message}");

            foreach (var item in clients)
            {
                if (item == client) continue;

                item.SendMessageAsync(format_message);
            }
        }

        public void RemoveClient(ClientHandler client)
        {
            clients.Remove(client);
            Console.WriteLine($"Client {client.username ?? client.client_id} disconnected. Total clients: {clients.Count}");
        }
    }
}
