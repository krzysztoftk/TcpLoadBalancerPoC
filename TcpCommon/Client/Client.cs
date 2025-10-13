using System.Net.Sockets;
using System.Text;
using Serilog;
using TcpCommon.Backend.ProtocolHandling;

namespace TcpCommon.Client;

public class Client : IClient
{
    private readonly ILogger _log = Log.ForContext<Client>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ClientConfiguration _clientConfiguration;
    private readonly TcpClient _client = new();
    private readonly IProtocolHandler _protocolHandler;
    private NetworkStream _networkStream;
    private bool _isRunning;


    public Client(ClientConfiguration clientConfiguration, IProtocolHandler protocolHandler)
    {
        _clientConfiguration = clientConfiguration;
        _protocolHandler = protocolHandler;
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _client.ConnectAsync(_clientConfiguration.GetBackendEndpoint());
            _log.Information($"{_clientConfiguration.Name} connected to: {_clientConfiguration.GetBackendEndpoint()}");
            _networkStream = _client.GetStream();
            _isRunning = true;
            _ = Task.Run(ReceiveMessagesAsync);
        }
        catch (Exception exception)
        {
            _log.Error($"{_clientConfiguration.Name} connection to server failed: {exception.Message}");
            _isRunning = false;
            throw;
        }

    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        await _protocolHandler.HandleSendAsync(message, _networkStream, cancellationToken);
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
    }

    private async Task ReceiveMessagesAsync()
    {
        const int bufferSize = 8192; 
        byte[] buffer = new byte[bufferSize];
        MemoryStream messageBuffer = new();

        try
        {
            while (_isRunning)
            {
                int bytesRead = await _networkStream.ReadAsync(buffer, 0, bufferSize, _cancellationTokenSource.Token);
                if (bytesRead == 0)
                {
                    break; // server closed connection
                }

                messageBuffer.Write(buffer, 0, bytesRead);

                if (CheckForCompleteMessage(buffer, bytesRead))
                {
                    string received = Encoding.UTF8.GetString(messageBuffer.ToArray()).Trim();
                    Console.WriteLine($"Server to {_clientConfiguration.Name}: {received}");
                    messageBuffer.SetLength(0);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.Information($"{_clientConfiguration.Name} receive operation cancelled.");
        }
        catch (IOException)
        {
            _log.Error($"{_clientConfiguration.Name} disconnected from server.");
        }
        finally
        {
            _isRunning = false;
            await messageBuffer.DisposeAsync();
        }
    }

    private bool CheckForCompleteMessage(byte[] buffer, int bytesRead) => buffer[bytesRead - 1] == '\n';
}