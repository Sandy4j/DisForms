using DisServer;

Server server = new Server("127.0.0.1", 8888);
await server.StartAsync();