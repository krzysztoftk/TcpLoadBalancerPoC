namespace TcpCommon.Backend;

public interface IServer
{
    Task StartAsync();

    Task StopAsync();
}