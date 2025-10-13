using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Client;
using TcpCommon.Infrastructure;

Logging.Configure();

Console.WriteLine("Client 2");

Thread.Sleep(1000);

ClientConfiguration clientConfiguration = new()
{
    Name = "Client 2",
    BackendIP = "127.0.0.1",
    BackendPort = 5000
};

Client client = new(clientConfiguration, new NewlineDelimitedProtocolHandler());

Task.Run(() =>
{
    _ = client.ConnectAsync();
});

await Task.Delay(1000);

await client.SendMessageAsync("Message F1");
await client.SendMessageAsync("Message F2");
await client.SendMessageAsync("Message F3");

while (true)
{
    await client.SendMessageAsync($"Message F + {Guid.NewGuid()}");
    Task.Delay(1000);
}


Console.ReadKey();