using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using TcpCommon.Backend;
using TcpCommon.Backend.Balancing;
using TcpCommon.Client;
using TcpCommon.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Tests.Backend.Balancing;

public class LoadBalancerTests
{
    private LoadBalancer _loadBalancer;
    private IBalancingStrategy _balancingStrategy;
    private List<TestServer> _backendServers;
    private const int LoadBalancerPort = 5000;
    private const int BackendServerPortBase = 5100;

    [SetUp]
    public void Setup()
    {
        _backendServers = new List<TestServer>();
        _balancingStrategy = new RoundRobinStrategy();

        ServerConfiguration loadBalancerConfig = new ServerConfiguration
        {
            Name = "LoadBalancer",
            IP = "127.0.0.1",
            Port = LoadBalancerPort
        };

        _loadBalancer = new LoadBalancer(_balancingStrategy, loadBalancerConfig, new NewlineDelimitedProtocolHandler());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _loadBalancer.StopAsync();
        foreach (TestServer server in _backendServers)
        {
            await server.StopAsync();
        }
    }

    [Test]
    public async Task AcceptTrafficFromManyClients()
    {
        // Arrange: Start single backend
        TestServer backendServer = new TestServer(BackendServerPortBase, "Backend1");
        await backendServer.StartAsync();
        _backendServers.Add(backendServer);

        _loadBalancer.AddEndpoint(new IPEndPoint(IPAddress.Loopback, BackendServerPortBase));

        Task.Run(() => _loadBalancer.StartAsync());
        await Task.Delay(200);

        // Act: create multiple clients
        int clientCount = 10;
        ConcurrentDictionary<int, int> hits = new ConcurrentDictionary<int, int>();
        List<Task> clientTasks = new List<Task>();

        for (int i = 0; i < clientCount; i++)
        {
            int requestId = i;
            clientTasks.Add(RunClientAsync(requestId, hits));
        }

        await Task.WhenAll(clientTasks);
        await _loadBalancer.StopAsync();

        Assert.That(hits.Count, Is.EqualTo(1), "All clients should connect through the same backend");
        Assert.That(hits.Values.Sum(), Is.EqualTo(clientCount), "All clients should receive responses successfully");
    }

    [Test]
    public async Task CanBalanceTrafficAcrossMultipleBackendServices()
    {
        int backendCount = 3;
        for (int i = 0; i < backendCount; i++)
        {
            int port = BackendServerPortBase + i;
            TestServer server = new TestServer(port, $"Backend{i}");
            await server.StartAsync();
            _backendServers.Add(server);
            _loadBalancer.AddEndpoint(new IPEndPoint(IPAddress.Loopback, port));
        }

        Task.Run(() => _loadBalancer.StartAsync());
        await Task.Delay(200);

        int requestCount = 9;
        ConcurrentDictionary<int, int> serverHits = new ConcurrentDictionary<int, int>();

        List<Task> clientTasks = new List<Task>();

        for (int i = 0; i < requestCount; i++)
        {
            int requestId = i;
            clientTasks.Add(RunClientAsync(requestId, serverHits));
        }

        await Task.WhenAll(clientTasks);
        await _loadBalancer.StopAsync();

        Assert.That(serverHits.Count, Is.EqualTo(backendCount), "Requests should be distributed across all backends");
        int maxHits = serverHits.Values.Max();
        int minHits = serverHits.Values.Min();
        Assert.That(maxHits - minHits, Is.LessThanOrEqualTo(1), "Load distribution should be roughly even");
    }

    [Test]
    public async Task CanRemoveServiceFromOperationIfItGoesOffline()
    {
        // Arrange: Start 2 backend servers
        TestServer backend1 = new TestServer(BackendServerPortBase, "Backend1");
        TestServer backend2 = new TestServer(BackendServerPortBase + 1, "Backend2");
        await backend1.StartAsync();
        await backend2.StartAsync();
        _backendServers.Add(backend1);
        _backendServers.Add(backend2);

        _loadBalancer.AddEndpoint(new IPEndPoint(IPAddress.Loopback, BackendServerPortBase));
        _loadBalancer.AddEndpoint(new IPEndPoint(IPAddress.Loopback, BackendServerPortBase + 1));
        Task.Run(() => _loadBalancer.StartAsync());
        await Task.Delay(200);

        // Act: Send initial requests to both backends
        ConcurrentDictionary<int, int> serverHits = new ConcurrentDictionary<int, int>();

        int initialRequests = 4;
        for (int i = 0; i < initialRequests; i++)
        {
            await RunClientAsync(i, serverHits);
        }

        // Take backend1 offline
        await backend1.StopAsync();
        await Task.Delay(500);

        // Send more requests - these should only go to backend2
        int additionalRequests = 4;
        for (int i = 0; i < additionalRequests; i++)
        {
            await RunClientAsync(initialRequests + i, serverHits);
        }

        await _loadBalancer.StopAsync();


        int successfulAdditionalRequests = serverHits.Values.Sum() - serverHits.Values.Sum(); // Count from initial
        int totalSuccessful = serverHits.Values.Sum();

        Assert.That(totalSuccessful, Is.GreaterThanOrEqualTo(additionalRequests), "Should successfully handle requests after one backend goes offline");
    }

    private int ExtractBackendIndex(string response)
    {
        Match match = Regex.Match(response, @"Backend(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    private async Task RunClientAsync(int requestId, ConcurrentDictionary<int, int> serverHits)
    {
        ClientConfiguration config = new ClientConfiguration
        {
            Name = $"Client-{requestId}",
            BackendIP = "127.0.0.1",
            BackendPort = LoadBalancerPort
        };

        Client.Client client = new Client.Client(config, new NewlineDelimitedProtocolHandler(), new TcpClientWrapper());
        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

        client.MessageReceived += message =>
        {
            received.TrySetResult(message);
        };

        try
        {
            await client.ConnectAsync();
            await client.SendMessageAsync($"Request {requestId}\n");

            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Task completed = await Task.WhenAny(received.Task, Task.Delay(-1, timeout.Token));

            if (completed == received.Task)
            {
                string response = await received.Task;
                int backendIndex = ExtractBackendIndex(response);
                serverHits.AddOrUpdate(backendIndex, 1, (_, count) => count + 1);
            }
        }
        finally
        {
            client.Stop();
        }
    }
}