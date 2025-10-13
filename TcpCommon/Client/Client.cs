using Serilog;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Client;

public class Client : IClient
{
    private readonly ILogger _log = Log.ForContext<Client>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ClientConfiguration _clientConfiguration;
    private readonly ITcpClient _tcpClient;
    private readonly IProtocolHandler _protocolHandler;
    private bool _isRunning;


    public Client(ClientConfiguration clientConfiguration, IProtocolHandler protocolHandler, ITcpClient tcpClient)
    {
        _clientConfiguration = clientConfiguration;
        _protocolHandler = protocolHandler;
        _tcpClient = tcpClient;
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _tcpClient.ConnectAsync(_clientConfiguration.GetBackendEndpoint(), _cancellationTokenSource.Token);
            _log.Information($"{_clientConfiguration.Name} connected to: {_clientConfiguration.GetBackendEndpoint()}");
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
        await _protocolHandler.HandleSendAsync(message, _tcpClient.GetStream(), cancellationToken);
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task ReceiveMessagesAsync()
    {
        try
        {
            while (_isRunning)
            {
                await _protocolHandler.HandleReceiveAsync(_tcpClient.GetStream(), _cancellationTokenSource.Token);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }
}