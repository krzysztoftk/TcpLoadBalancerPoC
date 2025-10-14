using Serilog;
using TcpCommon.Backend;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Client;

public class Client : IClient
{
    private readonly ILogger _log = Log.ForContext<Client>();
    private readonly ClientConfiguration _clientConfiguration;
    private readonly ITcpClient _tcpClient;
    private readonly IProtocolHandler _protocolHandler;
    private CancellationTokenSource? _cancellationTokenSource;
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
            _cancellationTokenSource = new CancellationTokenSource();
            await _tcpClient.ConnectAsync(_clientConfiguration.GetBackendEndpoint(), _cancellationTokenSource.Token);
            _log.Information("{ClientName} connected to: {BackendEndpoint}", _clientConfiguration.Name, _clientConfiguration.GetBackendEndpoint());
            _isRunning = true;
            _ = Task.Run(ReceiveMessagesAsync);
        }
        catch (Exception exception)
        {
            _log.Error("{ClientName} connection to server failed: {ExceptionMessage}", _clientConfiguration.Name, exception.Message);
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
        if (_isRunning is false)
        {
            return;
        }

        try
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
        }
        catch (Exception ex)
        {
            _log.Error("Error stopping client {ClientName}: {ExceptionMessage}", _clientConfiguration.Name, ex.Message);
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

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