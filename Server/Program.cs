using TcpCommon.Backend;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Infrastructure;
using TcpCommon.Wrappers;

Logging.Configure();

ServerConfiguration configuration1 = new()
{
    IP = "127.0.0.1",
    Name = "Server 1",
    Port = 4001
};

IServer server1 = new Server(configuration1, new NewlineDelimitedProtocolHandler(), new TcpListenerWrapper(configuration1.GetEndpoint()));


ServerConfiguration configuration2 = new()
{
    IP = "127.0.0.1",
    Name = "Server 2",
    Port = 4002
};

IServer server2 = new Server(configuration2, new NewlineDelimitedProtocolHandler(), new TcpListenerWrapper(configuration2.GetEndpoint()));

// Start each server on its own background task
_ = Task.Run(() => server1.StartAsync());
_ = Task.Run(() => server2.StartAsync());

Console.WriteLine("Both servers started. Press any key to stop...");
Console.ReadKey();

await server1.StopAsync();
await server2.StopAsync();