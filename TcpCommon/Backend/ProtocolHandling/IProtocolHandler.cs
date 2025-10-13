using System.Net.Sockets;

namespace TcpCommon.Backend.ProtocolHandling;

public interface IProtocolHandler
{
    Task HandleAsync(NetworkStream source, NetworkStream destination, CancellationToken cancellationToken);
}