using Serilog;
using System.Net.Sockets;
using System.Text;
using TcpCommon.Backend.ProtocolHandling;

namespace TcpCommon.Backend;

public class Server : IServer
{
    private readonly ILogger _log = Log.ForContext<Server>();
    private readonly ServerConfiguration _serverConfiguration;
    private readonly TcpListener _tcpListener;
    private bool _isRunning;
    private readonly IProtocolHandler _protocolHandler;

    public Server(
        ServerConfiguration serverConfiguration, 
        IProtocolHandler protocolHandler)
    {
        _serverConfiguration = serverConfiguration;
        _tcpListener = new(serverConfiguration.GetEndpoint());
        _protocolHandler = protocolHandler;
    }

    public void Start()
    {
        _tcpListener.Start();
        _isRunning = true;

        _log.Information($"Server {_serverConfiguration.Name} started on {_tcpListener.LocalEndpoint}");
        while (_isRunning)
        {
            try
            {
                TcpClient client = _tcpListener.AcceptTcpClient();
                _log.Information($"Client connected: {client.Client.RemoteEndPoint}");
                _ = Task.Run(() => HandleClient(client));
            }
            catch (Exception ex)
            {
                _log.Error($"{_serverConfiguration.Name}: Error accepting client: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _tcpListener.Stop();
    }

    private async Task HandleClient(TcpClient client)
    {
        NetworkStream clientStream = client.GetStream();
        CancellationTokenSource cancellationTokenSource = new();

        try
        {
            _ = Task.Run(async () =>
            {
                while (client.Connected && cancellationTokenSource.Token.IsCancellationRequested is false)
                {
                    try
                    {
                        string heartbeatMessage =
                            $"[Heartbeat from {_serverConfiguration.Name}] {DateTime.UtcNow:HH:mm:ss}\n";
                        byte[] messageBytes = Encoding.UTF8.GetBytes(heartbeatMessage);
                        await clientStream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationTokenSource.Token);
                        await clientStream.FlushAsync(cancellationTokenSource.Token);
                        await Task.Delay(TimeSpan.FromSeconds(60), cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _log.Information("Heartbeat loop cancelled for {Client}", client.Client.RemoteEndPoint);
                    }
                }
            }, cancellationTokenSource.Token);

            await _protocolHandler.HandleAsync(clientStream, clientStream, cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _log.Error($"{_serverConfiguration.Name}: Error handling client: {ex.Message}");
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();
            client.Close();
            _log.Information($"{_serverConfiguration.Name}: Client disconnected");
        }
    }
}