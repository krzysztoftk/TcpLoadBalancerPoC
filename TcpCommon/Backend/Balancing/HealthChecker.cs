using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Serilog;

namespace TcpCommon.Backend.Balancing;

public interface IHealthChecker
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    void RegisterEndpoint(IPEndPoint endpoint);
    void UnregisterEndpoint(IPEndPoint endpoint);
    IReadOnlyList<EndpointHealthStatus> GetHealthStatuses();
}

public class HealthChecker : IHealthChecker
{
    private readonly ILogger _log = Log.ForContext<HealthChecker>();
    private readonly ConcurrentDictionary<string, EndpointHealthStatus> _endpoints = new();
    private readonly int _healthCheckIntervalMs;
    private readonly int _healthCheckTimeoutMs;
    private readonly int _failureThreshold;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _healthCheckTask;

    public HealthChecker(
        int healthCheckIntervalMs = 5000,
        int healthCheckTimeoutMs = 2000,
        int failureThreshold = 3)
    {
        _healthCheckIntervalMs = healthCheckIntervalMs;
        _healthCheckTimeoutMs = healthCheckTimeoutMs;
        _failureThreshold = failureThreshold;
    }

    public void RegisterEndpoint(IPEndPoint endpoint)
    {
        string key = endpoint.ToString();
        _endpoints.TryAdd(key, new(endpoint));
        _log.Information("Registered endpoint for health checks: {Endpoint}", key);
    }

    public void UnregisterEndpoint(IPEndPoint endpoint)
    {
        string key = endpoint.ToString();
        _endpoints.TryRemove(key, out _);
        _log.Information("Unregistered endpoint from health checks: {Endpoint}", key);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _healthCheckTask = PerformHealthChecksAsync(_cancellationTokenSource.Token);
        _log.Information("Health checker started");
    }

    public async Task StopAsync()
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            if (_healthCheckTask is not null)
            {
                try
                {
                    await _healthCheckTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
        }
        _log.Information("Health checker stopped");
    }

    public IReadOnlyList<EndpointHealthStatus> GetHealthStatuses()
    {
        return _endpoints.Values.ToList().AsReadOnly();
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_healthCheckIntervalMs, cancellationToken);

                List<Task> checkTasks = _endpoints.Values
                    .Select(status => CheckEndpointHealthAsync(status, cancellationToken))
                    .ToList();

                await Task.WhenAll(checkTasks);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Information("Health check loop cancelled");
        }
    }

    private async Task CheckEndpointHealthAsync(EndpointHealthStatus status, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(_healthCheckTimeoutMs);

            using TcpClient client = new();
            await client.ConnectAsync(status.Endpoint.Address, status.Endpoint.Port, cancellationTokenSource.Token);

            status.IsHealthy = true;
            status.ConsecutiveFailures = 0;
            status.LastHealthCheckTime = DateTime.UtcNow;
            _log.Debug("Health check passed for endpoint: {Endpoint}", status.Endpoint);
        }
        catch (Exception ex)
        {
            status.ConsecutiveFailures++;
            status.LastHealthCheckTime = DateTime.UtcNow;

            if (status.ConsecutiveFailures >= _failureThreshold)
            {
                bool wasHealthy = status.IsHealthy;
                status.IsHealthy = false;

                if (wasHealthy)
                {
                    _log.Warning(
                        "Endpoint marked as unhealthy after {FailureCount} failures: {Endpoint} ({Exception})",
                        status.ConsecutiveFailures,
                        status.Endpoint,
                        ex.Message);
                }
            }
            else
            {
                _log.Debug(
                    "Health check failed for endpoint (attempt {FailureCount}/{Threshold}): {Endpoint}",
                    status.ConsecutiveFailures,
                    _failureThreshold,
                    status.Endpoint);
            }
        }
    }
}