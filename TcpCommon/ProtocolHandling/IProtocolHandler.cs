using TcpCommon.Wrappers;

namespace TcpCommon.ProtocolHandling;

public interface IProtocolHandler
{
    Task<string?> HandleReceiveAsync(INetworkStream sourceStream, CancellationToken cancellationToken);

    Task HandleSendAsync(string messageToSend, INetworkStream destinationStream, CancellationToken cancellationToken);
}