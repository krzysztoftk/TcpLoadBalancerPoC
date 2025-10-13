using System.Net.Sockets;
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
        try
        {
            while (_isRunning)
            {
                await _protocolHandler.HandleReceiveAsync(_networkStream, _cancellationTokenSource.Token);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }
}