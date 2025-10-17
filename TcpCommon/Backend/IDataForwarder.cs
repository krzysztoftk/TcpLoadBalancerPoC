using TcpCommon.Wrappers;

namespace TcpCommon.Backend;

public interface IDataForwarder
{
    Task ForwardAsync(INetworkStream source, INetworkStream destination, CancellationToken cancellationToken);
}