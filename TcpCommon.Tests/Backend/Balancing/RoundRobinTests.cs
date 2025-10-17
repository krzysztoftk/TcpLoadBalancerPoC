using System.Net;
using TcpCommon.Backend.Balancing;

namespace TcpCommon.Tests.Backend.Balancing;

public class RoundRobinTests
{
    private readonly RoundRobinStrategy _roundRobinStrategy = new RoundRobinStrategy();

    [Test]
    public void GetNext_EndpointCollectionEmpty_ReturnsNull()
    {
        IPEndPoint? nextEndppoint = _roundRobinStrategy.GetNext(new List<EndpointHealthStatus>());

        Assert.That(nextEndppoint, Is.Null);
    }


    [Test]
    public void GetNext_NoHealthyEndpoints_ReturnsNull()
    {
        List<EndpointHealthStatus> endpoints = new List<EndpointHealthStatus>()
        {
            new(new IPEndPoint(IPAddress.Loopback, 1000)),
            new(new IPEndPoint(IPAddress.Loopback, 1010)),
            new(new IPEndPoint(IPAddress.Loopback, 1020))
        };

        endpoints.ForEach(x => x.IsHealthy = false);

        IPEndPoint? nextEndppoint = _roundRobinStrategy.GetNext(endpoints);

        Assert.That(nextEndppoint, Is.Null);
    }

    [Test]
    public void GetNext_HealthyEndpointsPresent_ReturnsEndpoint()
    {
        List<EndpointHealthStatus> endpoints = new List<EndpointHealthStatus>()
        {
            new(new IPEndPoint(IPAddress.Loopback, 1000)),
            new(new IPEndPoint(IPAddress.Loopback, 1010)),
            new(new IPEndPoint(IPAddress.Loopback, 1020))
        };

        IPEndPoint? nextEndppoint = _roundRobinStrategy.GetNext(endpoints);

        Assert.That(nextEndppoint.Port, Is.EqualTo(1000));

        nextEndppoint = _roundRobinStrategy.GetNext(endpoints);

        Assert.That(nextEndppoint.Port, Is.EqualTo(1010));

        nextEndppoint = _roundRobinStrategy.GetNext(endpoints);

        Assert.That(nextEndppoint.Port, Is.EqualTo(1020));

        nextEndppoint = _roundRobinStrategy.GetNext(endpoints);

        Assert.That(nextEndppoint.Port, Is.EqualTo(1000));
    }
}