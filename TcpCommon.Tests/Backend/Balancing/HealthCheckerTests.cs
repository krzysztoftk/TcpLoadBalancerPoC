using System.Net;
using System.Net.Sockets;
using TcpCommon.Backend.Balancing;

namespace TcpCommon.Tests.Backend.Balancing;

public class HealthCheckerTests
{
    private readonly IPEndPoint _healthyEndpoint = new IPEndPoint(IPAddress.Loopback, 5555);
    private readonly IPEndPoint _unreachableEndpoint = new IPEndPoint(IPAddress.Loopback, 5999);

    private HealthChecker _healthChecker;

    [SetUp]
    public void SetUp()
    {
        _healthChecker = new HealthChecker(healthCheckIntervalMs: 100, healthCheckTimeoutMs: 100, failureThreshold: 2);
    }

    [Test]
    public async Task RegisterEndpoint_WhenCalled_ShouldAddEndpointToHealthStatuses()
    {
        _healthChecker.RegisterEndpoint(_healthyEndpoint);

        IReadOnlyList<EndpointHealthStatus> result = _healthChecker.GetHealthStatuses();

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result.First().Endpoint, Is.EqualTo(_healthyEndpoint));
    }

    [Test]
    public async Task CheckEndpointHealthAsync_WhenEndpointIsUnreachable_ShouldMarkAsUnhealthy()
    {
        _healthChecker.RegisterEndpoint(_unreachableEndpoint);
        EndpointHealthStatus status = _healthChecker.GetHealthStatuses().First();

        await InvokePrivateCheckAsync(_healthChecker, status); // 1st failure
        await InvokePrivateCheckAsync(_healthChecker, status); // 2nd failure

        Assert.That(status.IsHealthy, Is.False);
        Assert.That(status.ConsecutiveFailures, Is.EqualTo(2));
    }

    [Test]
    public async Task CheckEndpointHealthAsync_WhenEndpointIsReachable_ShouldMarkAsHealthy()
    {
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 5555);
        listener.Start();

        _healthChecker.RegisterEndpoint(_healthyEndpoint);
        EndpointHealthStatus status = _healthChecker.GetHealthStatuses().First();

        await InvokePrivateCheckAsync(_healthChecker, status);

        Assert.That(status.IsHealthy, Is.True);
        Assert.That(status.ConsecutiveFailures, Is.EqualTo(0));

        listener.Stop();
    }

    private static async Task InvokePrivateCheckAsync(HealthChecker checker, EndpointHealthStatus status)
    {
        System.Reflection.MethodInfo? method = typeof(HealthChecker)
            .GetMethod("CheckEndpointHealthAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method.Invoke(checker, new object[] { status, CancellationToken.None });
    }
}