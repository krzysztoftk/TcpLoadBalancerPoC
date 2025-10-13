namespace TcpCommon.Wrappers;

public interface INetworkStream : IDisposable
{
    Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken token);
    Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken token);
    Task FlushAsync(CancellationToken token);
}