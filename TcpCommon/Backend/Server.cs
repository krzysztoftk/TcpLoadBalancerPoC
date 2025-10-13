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
    private bool _isRunning;
    private readonly IProtocolHandler _protocolHandler;

    public Server(
        ServerConfiguration serverConfiguration, 
        IProtocolHandler protocolHandler,
        ITcpListener tcpListener)
    {
        _serverConfiguration = serverConfiguration;
        _tcpListener = tcpListener;
        _protocolHandler = protocolHandler;
    }

    public async Task StartAsync()
    {
        _tcpListener.Start();
        _isRunning = true;
        _log.Information("Server {ServerName} started listening on {Endpoint}", _serverConfiguration.Name, _tcpListener.LocalEndpoint);

        while (_isRunning)
        {
            try
            {
                ITcpClient client = await _tcpListener.AcceptTcpClientAsync(default); //todo: use CancelationTokenSource
                _log.Information($"Client connected: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClient(client));
            }
            catch (Exception ex)
            {
                _log.Error("{ServerName}: Error accepting client: {ExceptionMessage}", _serverConfiguration.Name, ex.Message);
            }
        }
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _tcpListener.Stop();
        return Task.CompletedTask; //todo
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