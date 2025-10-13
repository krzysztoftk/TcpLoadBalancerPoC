using System.Text;
using Serilog;
using TcpCommon.Wrappers;

namespace TcpCommon.Backend.ProtocolHandling;

public class NewlineDelimitedProtocolHandler : IProtocolHandler
{
    private readonly ILogger _log = Log.ForContext<NewlineDelimitedProtocolHandler>();
    private const int BufferSize = 8192;

    public async Task HandleReceiveAsync(INetworkStream sourceStream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];
        MemoryStream messageBuffer = new();

        try
        {
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, BufferSize, cancellationToken)) > 0)
            {
                messageBuffer.Write(buffer, 0, bytesRead);

                if (CheckForCompleteMessage(buffer, bytesRead) is false)
                {
                    continue;
                }

                string message = Encoding.UTF8.GetString(messageBuffer.ToArray()).Trim();
                _log.Information($"Received message: {message}");

                messageBuffer.SetLength(0);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Information("Receive operation cancelled");
        }
        catch (IOException ex)
        {
            _log.Warning(ex, "IO error during receive");
        }
        catch (ObjectDisposedException ex)
        {
            _log.Warning(ex, "Stream disposed during receive");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error during receive");
        }
        finally
        {
            await messageBuffer.DisposeAsync();
        }
    }

    public async Task HandleSendAsync(string messageToSend, INetworkStream destinationStream, CancellationToken cancellationToken)
    {
        try
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(messageToSend + "\n");
            await destinationStream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _log.Information("Send operation cancelled");
        }
        catch (IOException ex)
        {
            _log.Warning(ex, "IO error during send: {Message}", messageToSend);
        }
        catch (ObjectDisposedException ex)
        {
            _log.Warning(ex, "Stream disposed during send");
        }
        catch (ArgumentException ex)
        {
            _log.Error(ex, "Invalid arguments during send");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unexpected error during send: {Message}", messageToSend);
        }
    }

    private bool CheckForCompleteMessage(byte[] buffer, int bytesRead) => buffer[bytesRead - 1] == '\n';
}