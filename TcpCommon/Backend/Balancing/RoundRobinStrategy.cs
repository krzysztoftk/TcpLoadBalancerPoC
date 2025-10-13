using System.Net;

namespace TcpCommon.Backend.Balancing;

public interface IBalancingStrategy
{
    IPEndPoint? GetNext(IEnumerable<EndpointHealthStatus> endpoints);
}

public class RoundRobinStrategy : IBalancingStrategy
{
    private readonly object _lockObj = new();
    private int _currentServerIndex = 0;

    public IPEndPoint? GetNext(IEnumerable<EndpointHealthStatus> endpoints)
    {
        lock (_lockObj)
        {
            List<EndpointHealthStatus> healthyEndpoints = endpoints
                .Where(s => s.IsHealthy)
                .ToList();

            if (healthyEndpoints.Count == 0)
            {
                return null;
            }

            EndpointHealthStatus selected = healthyEndpoints[_currentServerIndex % healthyEndpoints.Count];
            _currentServerIndex++;
            return selected.Endpoint;
        }
    }
}