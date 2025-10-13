using System.Net;

namespace TcpCommon.Wrappers;

public interface ITcpListener : IDisposable
{
    void Start();
    void Stop();
    Task<ITcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken);
    EndPoint LocalEndpoint { get; }
}