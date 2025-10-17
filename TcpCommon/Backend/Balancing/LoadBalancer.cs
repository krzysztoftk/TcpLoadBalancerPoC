using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using TcpCommon.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Backend.Balancing;

public class LoadBalancer : ILoadBalancer
{
    private readonly ILogger _log = Log.ForContext<LoadBalancer>();
    private readonly ServerConfiguration _configuration;
    private readonly ITcpListener _tcpListener;
    private readonly IBalancingStrategy _balancingStrategy;
    private readonly IHealthChecker _healthChecker;
    private readonly IDataForwarder _dataForwarder;
    private readonly ConcurrentBag<EndpointHealthStatus> _endpoints = new();
    private readonly IProtocolHandler _protocolHandler;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptClientsTask;
    private bool _isRunning;

    public LoadBalancer(
        IBalancingStrategy balancingStrategy,
        ServerConfiguration configuration, 
        IProtocolHandler protocolHandler, 
        IHealthChecker? healthChecker = null,
        IDataForwarder? dataForwarder = null)
    {
        _balancingStrategy = balancingStrategy;
        _configuration = configuration;
        _tcpListener = new TcpListenerWrapper(_configuration.GetEndpoint());
        _protocolHandler = protocolHandler;
        _healthChecker = healthChecker ?? new HealthChecker();
        _dataForwarder = dataForwarder ?? new SimpleDataForwarder();
    }

    public void AddEndpoint(IPEndPoint endpoint)
    {
        EndpointHealthStatus status = new(endpoint);
        _endpoints.Add(status);
        _healthChecker.RegisterEndpoint(endpoint);
        _log.Information("Added endpoint: {Endpoint}", endpoint);
    }

    public async Task StartAsync()
    {
        try
        {
            _cancellationTokenSource = new();
            _isRunning = true;

            _tcpListener.Start();
            await _healthChecker.StartAsync(_cancellationTokenSource.Token);
            _log.Information("LoadBalancer listening on: {Endpoint}", _configuration.GetEndpoint());
            _acceptClientsTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            await _acceptClientsTask;
        }
        catch (OperationCanceledException)
        {
            _log.Information("Load balancer startup cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error starting load balancer");
            _isRunning = false;
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_isRunning is false)
        {
            return;
        }

        _isRunning = false;

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        try
        {
            _tcpListener.Stop();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error stopping listener");
        }

        if (_acceptClientsTask is not null)
        {
            try
            {
                await _acceptClientsTask;
            }
            catch (OperationCanceledException)
            {
                _log.Debug("Load balancer: Client acceptance loop stopped due to cancellation.");
            }
            catch (ObjectDisposedException)
            {
                _log.Debug("Listener disposed during stop");
            }
        }

        await _healthChecker.StopAsync();
        _log.Information("Load balancer stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_isRunning && cancellationToken.IsCancellationRequested is false)
            {
                ITcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                _log.Information("Client connected: {RemoteEndpoint}", tcpClient.Client.RemoteEndPoint);

                _ = HandleClientAsync(tcpClient, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Debug("Accept clients loop cancelled");
        }
        catch (ObjectDisposedException)
        {
            _log.Debug("Listener disposed");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error accepting client");
        }
    }

    private async Task HandleClientAsync(ITcpClient tcpClient, CancellationToken cancellationToken)
    {
        ITcpClient? backendClient = null;

        try
        {
            INetworkStream clientStream = tcpClient.GetStream();

            IPEndPoint? backendEndpoint = _balancingStrategy.GetNext(_endpoints);

            if (backendEndpoint is null)
            {
                _log.Warning("No healthy backends available");
                await _protocolHandler.HandleSendAsync("Service Unavailable", clientStream, cancellationToken);
                return;
            }

            backendClient = new TcpClientWrapper(new TcpClient());

            try
            {
                await backendClient.ConnectAsync(backendEndpoint, cancellationToken);
                _log.Debug("Connected to backend: {Endpoint}", backendEndpoint);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to connect to backend {Endpoint}, trying next endpoint", backendEndpoint);

                backendEndpoint = _balancingStrategy.GetNext(_endpoints);
                if (backendEndpoint == null)
                {
                    _log.Warning("No more healthy backends available after failure");
                    await _protocolHandler.HandleSendAsync("Service Unavailable", clientStream, cancellationToken);
                    return;
                }

                backendClient = new TcpClientWrapper(new TcpClient());
                await backendClient.ConnectAsync(backendEndpoint, cancellationToken);
            }

            INetworkStream backendStream = backendClient.GetStream();

            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task forwardClientToBackend = _dataForwarder.ForwardAsync(clientStream, backendStream, cancellationTokenSource.Token);
            Task forwardBackendToClient = _dataForwarder.ForwardAsync(backendStream, clientStream, cancellationTokenSource.Token);

            _ = await Task.WhenAny(forwardClientToBackend, forwardBackendToClient);
            await cancellationTokenSource.CancelAsync();
        }
        catch (OperationCanceledException)
        {
            _log.Debug("Client handling cancelled");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error handling client");
        }
        finally
        {
            try
            {
                tcpClient.Close();
                backendClient?.Close();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error closing connections");
            }
        }
    }
}