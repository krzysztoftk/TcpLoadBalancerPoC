using System.Net.Sockets;
using Serilog;
using TcpCommon.Wrappers;

namespace TcpCommon.Backend;

public interface IDataForwarder
{
    Task ForwardAsync(INetworkStream source, INetworkStream destination, CancellationToken cancellationToken);
}

public class SimpleDataForwarder : IDataForwarder
{
    private readonly ILogger _log = Log.ForContext<SimpleDataForwarder>();
    private const int BufferSize = 8192;

    public async Task ForwardAsync(INetworkStream source, INetworkStream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, BufferSize, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }
        }
        catch (IOException)
        {
            _log.Debug("Connection closed");
        }
        catch (ObjectDisposedException)
        {
            _log.Debug("Stream disposed");
        }
        catch (OperationCanceledException)
        {
            _log.Information("Forwarding cancelled");
        }
    }
}