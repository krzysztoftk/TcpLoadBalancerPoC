using System.Text;
using TcpCommon.ProtocolHandling;
using TcpCommon.Wrappers;

namespace TcpCommon.Tests.ProtocolHandling;

public class NewlineDelimitedProtocolHandlerTests
{
    private NewlineDelimitedProtocolHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _handler = new NewlineDelimitedProtocolHandler();
    }

    [Test]
    public async Task HandleSendAsync_WithMessageStringProvided_ShouldWriteNewlineTerminatedMessage()
    {
        MemoryStream stream = new();
        INetworkStream fakeStream = new MemoryNetworkStream(stream);

        await _handler.HandleSendAsync("Test message", fakeStream, CancellationToken.None);

        string result = Encoding.UTF8.GetString(stream.ToArray());
        Assert.That(result, Is.EqualTo("Test message\n"));
    }

    [Test]
    public async Task HandleReceiveAsync_WithMessageStringProvided_ShouldReturnMessage()
    {
        string input = "Test message\n";
        MemoryStream stream = new(Encoding.UTF8.GetBytes(input));
        INetworkStream fakeStream = new MemoryNetworkStream(stream);

        string? message = await _handler.HandleReceiveAsync(fakeStream, CancellationToken.None);

        Assert.That(message, Is.EqualTo("Test message"));
    }

    [Test]
    public async Task HandleReceiveAsync_WithNoMessage_ShouldReturnNullOnEmptyStream()
    {
        MemoryStream stream = new();
        INetworkStream fakeStream = new MemoryNetworkStream(stream);

        string? message = await _handler.HandleReceiveAsync(fakeStream, CancellationToken.None);

        Assert.That(message, Is.Null);
    }
}