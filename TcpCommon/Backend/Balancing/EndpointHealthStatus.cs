using System.Net;

namespace TcpCommon.Backend.Balancing;

public class EndpointHealthStatus
{
    public IPEndPoint Endpoint { get; init; }
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheckTime { get; set; }
    public int ConsecutiveFailures { get; set; }

    public EndpointHealthStatus(IPEndPoint endpoint)
    {
        Endpoint = endpoint;
        IsHealthy = true;
        LastHealthCheckTime = DateTime.UtcNow;
        ConsecutiveFailures = 0;
    }
}