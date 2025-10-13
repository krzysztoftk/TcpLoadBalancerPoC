using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TcpCommon.Backend.Balancing;

public class LoadBalancer : ILoadBalancer
{
    private readonly ILogger _log = Log.ForContext<LoadBalancer>();
    private readonly ServerConfiguration _configuration;
    private readonly TcpListener _listener;
    private readonly IBalancingStrategy _balancingStrategy;
    private readonly IHealthChecker _healthChecker;
    private readonly IDataForwarder _dataForwarder;
    private readonly ConcurrentBag<EndpointHealthStatus> _endpoints = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptClientsTask;
    private bool _isRunning;

    public LoadBalancer(
        IBalancingStrategy balancingStrategy,
        ServerConfiguration configuration,
        IHealthChecker? healthChecker = null,
        IDataForwarder? dataForwarder = null)
    {
        _balancingStrategy = balancingStrategy;
        _configuration = configuration;
        _listener = new(configuration.GetEndpoint());
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

            await _healthChecker.StartAsync(_cancellationTokenSource.Token);

            _listener.Start();
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
        _isRunning = false;

        if (_cancellationTokenSource != null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        try
        {
            _listener.Stop();
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Error stopping listener");
        }

        if (_acceptClientsTask != null)
        {
            try
            {
                await _acceptClientsTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
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
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(cancellationToken);
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

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        TcpClient? backendClient = null;

        try
        {
            NetworkStream clientStream = tcpClient.GetStream();

            IPEndPoint? backendEndpoint = _balancingStrategy.GetNext(_endpoints);

            if (backendEndpoint == null)
            {
                _log.Warning("No healthy backends available");
                byte[] errorMessage = Encoding.UTF8.GetBytes("Service Unavailable\n");
                await clientStream.WriteAsync(errorMessage, 0, errorMessage.Length, cancellationToken);
                await clientStream.FlushAsync(cancellationToken);
                return;
            }

            backendClient = new();

            try
            {
                await backendClient.ConnectAsync(backendEndpoint.Address, backendEndpoint.Port, cancellationToken);
                _log.Debug("Connected to backend: {Endpoint}", backendEndpoint);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Failed to connect to backend {Endpoint}, trying next endpoint", backendEndpoint);

                backendEndpoint = _balancingStrategy.GetNext(_endpoints);
                if (backendEndpoint == null)
                {
                    _log.Warning("No more healthy backends available after failure");
                    byte[] errorMessage = Encoding.UTF8.GetBytes("Service Unavailable\n");
                    await clientStream.WriteAsync(errorMessage, 0, errorMessage.Length, cancellationToken);
                    await clientStream.FlushAsync(cancellationToken);
                    return;
                }

                backendClient = new();
                await backendClient.ConnectAsync(backendEndpoint.Address, backendEndpoint.Port, cancellationToken);
            }

            NetworkStream backendStream = backendClient.GetStream();

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