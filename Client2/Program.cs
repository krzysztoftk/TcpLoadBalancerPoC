using TcpCommon.Client;
using TcpCommon.Infrastructure;
using TcpCommon.ProtocolHandling;
using TcpCommon.Wrappers;

Logging.Configure();

Console.WriteLine("Client 2");

await Task.Delay(1000);

ClientConfiguration clientConfiguration = new()
{
    Name = "Client 2",
    BackendIP = "127.0.0.1",
    BackendPort = 5000
};

Client client = new(clientConfiguration, new NewlineDelimitedProtocolHandler(), new TcpClientWrapper());

Task.Run(() =>
{
    _ = client.ConnectAsync();
});

await Task.Delay(1000);

await client.SendMessageAsync($"{clientConfiguration.Name}: Message F1");
await client.SendMessageAsync($"{clientConfiguration.Name}: Message F2");
await client.SendMessageAsync($"{clientConfiguration.Name}: Message F3");

Console.WriteLine("Press any key to stop sending messages...");

while (!Console.KeyAvailable)
{
    await client.SendMessageAsync($"{clientConfiguration.Name}: Message F + {Guid.NewGuid()}");
    await Task.Delay(TimeSpan.FromSeconds(1));
}

Console.WriteLine("Key pressed. Stopping message loop.");

while (Console.KeyAvailable)
{
    Console.ReadKey();
}