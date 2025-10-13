using TcpCommon.Backend;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Infrastructure;

Logging.Configure();

ServerConfiguration configuration1 = new()
{
    IP = "127.0.0.1",
    Name = "Server 1",
    Port = 4001
};

IServer server1 = new Server(configuration1, new NewlineDelimitedProtocolHandler());


ServerConfiguration configuration2 = new()
{
    IP = "127.0.0.1",
    Name = "Server 2",
    Port = 4002
};

IServer server2 = new Server(configuration2, new NewlineDelimitedProtocolHandler());

// Start each server on its own background task
Task.Run(() => server1.Start());
Task.Run(() => server2.Start());

Console.WriteLine("Both servers started. Press any key to stop...");
Console.ReadKey();

server1.Stop();
server2.Stop();