using System.Net;

namespace TcpCommon.Tests;

public class FakeServer
{
    public IPEndPoint Endpoint { get; }
    public bool IsOnline { get; set; } = true;
    public int RequestCount { get; private set; }

    public FakeServer(IPEndPoint endpoint)
    {
        Endpoint = endpoint;
    }

    public Task HandleRequest(string message)
    {
        if (!IsOnline)
        {
            throw new("Server offline");
        }

        RequestCount++;
        return Task.CompletedTask;
    }
}