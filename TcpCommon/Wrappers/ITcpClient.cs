using System.Net;
using System.Net.Sockets;

namespace TcpCommon.Wrappers;

public interface ITcpClient : IDisposable
{
    bool Connected { get; }
    Socket Client { get; }
    Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);
    INetworkStream GetStream();
    void Close();
}