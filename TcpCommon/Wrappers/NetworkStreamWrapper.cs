using System.Net.Sockets;

namespace TcpCommon.Wrappers;

public class NetworkStreamWrapper : INetworkStream
{
    private readonly NetworkStream _stream;

    public NetworkStreamWrapper(NetworkStream stream)
    {
        _stream = stream;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken token)
        => await _stream.ReadAsync(buffer, offset, size, token);

    public async Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken token)
        => await _stream.WriteAsync(buffer, offset, size, token);

    public async Task FlushAsync(CancellationToken token)
        => await _stream.FlushAsync(token);

    public void Dispose() => _stream.Dispose();
}