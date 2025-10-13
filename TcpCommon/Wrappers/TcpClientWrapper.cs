using System.Net.Sockets;
using System.Net;

namespace TcpCommon.Wrappers;

public class TcpClientWrapper : ITcpClient
{
    private readonly TcpClient _client;

    public TcpClientWrapper()
    {
        _client = new TcpClient();
    }

    public TcpClientWrapper(TcpClient client)
    {
        _client = client;
    }

    public bool Connected => _client.Connected;

    public Socket Client => _client.Client;

    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
        => await _client.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken);

    public INetworkStream GetStream()
        => new NetworkStreamWrapper(_client.GetStream());

    public void Close() => _client.Close();

    public void Dispose() => _client.Dispose();
}