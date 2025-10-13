using System.Net;

namespace TcpCommon.Client;

public class ClientConfiguration
{
    public string Name { get; init; }
    public string BackendIP { get; init; } = "0.0.0.0";
    public int BackendPort { get; init; }
    public IPEndPoint GetBackendEndpoint() => new(IPAddress.Parse(BackendIP), BackendPort);
}