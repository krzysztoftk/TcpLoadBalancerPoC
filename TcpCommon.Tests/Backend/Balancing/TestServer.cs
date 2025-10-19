using System.Net;
using System.Net.Sockets;
using TcpCommon.Backend;

namespace TcpCommon.Tests.Backend.Balancing;

public class TestServer : IServer
{
    private readonly TcpListener _listener;
    private bool _isRunning;
    private readonly int _port;
    private readonly string _name;

    public TestServer(int port, string name)
    {
        _port = port;
        _name = name;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        _isRunning = true;
        _ = HandleClientsAsync();
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _listener.Stop();
        await Task.Delay(100);
    }

    public IPEndPoint Endpoint => new(IPAddress.Loopback, _port);

    private async Task HandleClientsAsync()
    {
        while (_isRunning)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await using NetworkStream stream = client.GetStream();
                        using StreamReader reader = new StreamReader(stream);
                        await using StreamWriter writer = new StreamWriter(stream);
                        string? line = await reader.ReadLineAsync();
                        if (line != null)
                        {
                            string response = $"ACK from {_name}\n";
                            Console.WriteLine(response);
                            await writer.WriteLineAsync(response);
                            await writer.FlushAsync();
                        }
                    }
                    catch { }
                    finally
                    {
                        client.Close();
                    }
                });
            }
            catch { }
        }
    }
}