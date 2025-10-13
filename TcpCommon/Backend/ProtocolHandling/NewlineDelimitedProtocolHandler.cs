using System.Net.Sockets;
using System.Numerics;
using System.Text;
using Serilog;

namespace TcpCommon.Backend.ProtocolHandling;

public class NewlineDelimitedProtocolHandler : IProtocolHandler
{
    private readonly ILogger _log = Log.ForContext<NewlineDelimitedProtocolHandler>();
    private const int BufferSize = 8192;

    public async Task HandleAsync(NetworkStream source, NetworkStream destination, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferSize];
        MemoryStream messageBuffer = new();

        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, BufferSize, cancellationToken)) > 0)
            {
                messageBuffer.Write(buffer, 0, bytesRead);

                if (CheckForCompleteMessage(buffer, bytesRead))
                {
                    string message = Encoding.UTF8.GetString(messageBuffer.ToArray()).Trim();
                    _log.Information($"Received message: {message}");

                    string response = $"ACK: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await destination.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                    await destination.FlushAsync(cancellationToken);

                    messageBuffer.SetLength(0);
                }
            }
        }
        finally
        {
            await messageBuffer.DisposeAsync();
        }
    }

    private bool CheckForCompleteMessage(byte[] buffer, int bytesRead) => buffer[bytesRead - 1] == '\n';
}