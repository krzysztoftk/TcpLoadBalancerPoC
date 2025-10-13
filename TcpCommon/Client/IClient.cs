namespace TcpCommon.Client;

public interface IClient
{
    Task ConnectAsync();
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
    void Stop();
}