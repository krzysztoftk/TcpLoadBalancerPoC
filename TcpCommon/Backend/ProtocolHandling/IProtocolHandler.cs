using System.Net.Sockets;

namespace TcpCommon.Backend.ProtocolHandling;

public interface IProtocolHandler
{
    Task HandleReceiveAsync(NetworkStream sourceStream, CancellationToken cancellationToken);

    Task HandleSendAsync(string messageToSend, NetworkStream destinationStream, CancellationToken cancellationToken);
}