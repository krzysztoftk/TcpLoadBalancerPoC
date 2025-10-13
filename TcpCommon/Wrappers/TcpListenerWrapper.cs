using System.Net;
using System.Net.Sockets;

namespace TcpCommon.Wrappers;

public class TcpListenerWrapper : ITcpListener
{
    private readonly TcpListener _listener;

    public TcpListenerWrapper(IPEndPoint endpoint)
    {
        _listener = new TcpListener(endpoint);
    }

    public void Start() => _listener.Start();

    public void Stop() => _listener.Stop();

    public async Task<ITcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken)
    {
        TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
        return new TcpClientWrapper(client);
    }

    public EndPoint LocalEndpoint => _listener.LocalEndpoint;

    public void Dispose() => _listener.Stop();
}