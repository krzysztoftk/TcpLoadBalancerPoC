using System.Net;
using TcpCommon.Backend;
using TcpCommon.Backend.Balancing;
using TcpCommon.Wrappers;

namespace TcpCommon.Tests;

public class Tests
{
    private LoadBalancer _loadBalancer;
    private List<FakeServer> _servers;


    [SetUp]
    public void Setup()
    {

        ServerConfiguration balancerConfiguration = new()
        {
            IP = "127.0.0.1",
            Name = "Server 2",
            Port = 5000
        };

        _servers = new()
        {
            new(new(IPAddress.Loopback, 6001)),
            new(new(IPAddress.Loopback, 6002)),
            new(new(IPAddress.Loopback, 6003))
        };

        _loadBalancer = new(
            new RoundRobinStrategy(),
            balancerConfiguration,
            new TcpListenerWrapper(balancerConfiguration.GetEndpoint()));

        foreach (FakeServer fakeServer in _servers)
        {
            _loadBalancer.AddEndpoint(fakeServer.Endpoint);
        }
    }

    [Test]
    public void AcceptTrafficFromManyClients()
    {
        Assert.Pass();
    }

    [Test]
    public void CanBalanceTrafficAcrossMultipleBackendServices()
    {
        Assert.Pass();
    }


    [Test] 
    public void CanRemoveServiceFromOperationIfItGoesOffline()
    {
        Assert.Pass();
    }
}