using TcpCommon.Wrappers;

namespace TcpCommon.Backend.ProtocolHandling;

public interface IProtocolHandler
{
    Task<string?> HandleReceiveAsync(INetworkStream sourceStream, CancellationToken cancellationToken);

    Task HandleSendAsync(string messageToSend, INetworkStream destinationStream, CancellationToken cancellationToken);
}