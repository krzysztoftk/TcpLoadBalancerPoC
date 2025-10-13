using System.Net;

namespace TcpCommon.Backend.Balancing;

public interface ILoadBalancer
{
    Task StartAsync();
    Task StopAsync();
    void AddEndpoint(IPEndPoint endpoint);
}