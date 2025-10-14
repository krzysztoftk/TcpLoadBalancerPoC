using System.Net;
using TcpCommon.Backend;
using TcpCommon.Backend.Balancing;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Infrastructure;

Logging.Configure();

Console.WriteLine("Load balancer");

RoundRobinStrategy roundRobinStrategy = new ();
ServerConfiguration balancerConfiguration = new()
{
    IP = "127.0.0.1",
    Name = "Server 2",
    Port = 5000
};
LoadBalancer loadBalancer = new(roundRobinStrategy, balancerConfiguration, new NewlineDelimitedProtocolHandler());

IPEndPoint server1Endpoint = new(IPAddress.Loopback, 4001);
IPEndPoint server2Endpoint = new(IPAddress.Loopback, 4002);
loadBalancer.AddEndpoint(server1Endpoint);
loadBalancer.AddEndpoint(server2Endpoint);

await loadBalancer.StartAsync();

Console.ReadKey();