using System.Reflection;
using TcpCommon.Wrappers;

namespace TcpCommon.Tests.ProtocolHandling;

internal class MemoryNetworkStream : INetworkStream
{
    private readonly MemoryStream _stream;

    public MemoryNetworkStream(MemoryStream stream)
    {
        _stream = stream;
    }

    public async Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken token)
        => await _stream.ReadAsync(buffer, offset, size, token);

    public async Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken token)
        => await _stream.WriteAsync(buffer, offset, size, token);

    public async Task FlushAsync(CancellationToken token)
        => await _stream.FlushAsync(token);

    public byte[] ToArray() => _stream.ToArray();

    public void Dispose() => _stream.Dispose();
}