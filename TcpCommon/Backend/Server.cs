using System.Net;
using Serilog;
using TcpCommon.Backend.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Backend;

public class Server : IServer
{
    private readonly ILogger _log = Log.ForContext<Server>();
    private readonly ServerConfiguration _serverConfiguration;
    private readonly ITcpListener _tcpListener;
    private readonly IProtocolHandler _protocolHandler;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;


    public Server(
        ServerConfiguration serverConfiguration, 
        IProtocolHandler protocolHandler)
    {
        _serverConfiguration = serverConfiguration;
        _tcpListener = new TcpListenerWrapper(_serverConfiguration.GetEndpoint());
        _protocolHandler = protocolHandler;
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _tcpListener.Start();
            _isRunning = true;
            _log.Information("Server {ServerName} started listening on {Endpoint}", _serverConfiguration.Name, _tcpListener.LocalEndpoint);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "{ServerName} failed to start listener on {Endpoint}", _serverConfiguration.Name, _serverConfiguration.GetEndpoint());
            return;
        }

        while (_isRunning && _cancellationTokenSource.IsCancellationRequested is false)
        {
            try
            {
                ITcpClient client = await _tcpListener.AcceptTcpClientAsync(_cancellationTokenSource.Token); 
                _log.Information($"Client connected: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClient(client));
            }
            catch (OperationCanceledException)
            {
                _log.Information("{ServerName}: Server stopping, accept loop cancelled", _serverConfiguration.Name);
            }
            catch (Exception ex)
            {
                _log.Error("{ServerName}: Error accepting client: {ExceptionMessage}", _serverConfiguration.Name, ex.Message);
            }
        }
    }

    public Task StopAsync()
    {
        if (_isRunning is false)
        {
            return Task.CompletedTask;
        }

        _isRunning = false;

        try
        {

            _cancellationTokenSource?.Cancel();
            _tcpListener.Stop();

            _log.Information("{ServerName}: Server stopped", _serverConfiguration.Name);
        }
        catch (Exception ex)
        {
            _log.Error("{ServerName}: Error stopping server: {ExceptionMessage}", _serverConfiguration.Name,
                ex.Message);
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        return Task.CompletedTask;
    }

    private async Task HandleClient(ITcpClient client)
    {
        INetworkStream clientStream = client.GetStream();
        CancellationTokenSource cancellationTokenSource = new();

        try
        {
            _ = Task.Run(async () =>
            {
                while (client.Connected && cancellationTokenSource.Token.IsCancellationRequested is false)
                {
                    await SendHeartbeatAsync(clientStream, client.Client.RemoteEndPoint,  cancellationTokenSource.Token);
                }
            }, cancellationTokenSource.Token);

            await _protocolHandler.HandleReceiveAsync(clientStream, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _log.Error("{ServerName}: Error handling client: {ExceptionMessage}", _serverConfiguration.Name, ex.Message);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            client.Close();
            _log.Information("{ServerName}: Client disconnected", _serverConfiguration.Name);
        }
    }

    private async Task SendHeartbeatAsync(INetworkStream clientStream, EndPoint? remoteEndPoint, CancellationToken cancellationToken)
    {
        try
        {
            string heartbeatMessage = $"[Heartbeat from {_serverConfiguration.Name}] {DateTime.UtcNow:HH:mm:ss}";
            await _protocolHandler.HandleSendAsync(heartbeatMessage, clientStream, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _log.Information("{ServerName} heartbeat loop cancelled for {ClientEndpoint}", _serverConfiguration.Name, remoteEndPoint);
        }
    }
}