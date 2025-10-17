using System.Net;

namespace TcpCommon.Backend.Balancing;

public interface IHealthChecker
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    void RegisterEndpoint(IPEndPoint endpoint);
    void UnregisterEndpoint(IPEndPoint endpoint);
    IReadOnlyList<EndpointHealthStatus> GetHealthStatuses();
}