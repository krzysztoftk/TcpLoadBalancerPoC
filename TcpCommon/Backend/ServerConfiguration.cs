using System.Net;

namespace TcpCommon.Backend;

public class ServerConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string IP { get; init; } = "0.0.0.0";
    public int Port { get; init; }
    public IPEndPoint GetEndpoint() => new(IPAddress.Parse(IP), Port);
}