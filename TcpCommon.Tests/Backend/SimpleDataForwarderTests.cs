using TcpCommon.Backend;
using TcpCommon.Tests.ProtocolHandling;

namespace TcpCommon.Tests.Backend;

public class SimpleDataForwarderTests
{
    private SimpleDataForwarder _forwarder;

    [SetUp]
    public void SetUp()
    {
        _forwarder = new SimpleDataForwarder();
    }

    [Test]
    public async Task ForwardAsync_WhenDataAvailable_ShouldTransferAllBytes()
    {
        byte[] sourceData = [1, 2, 3, 4, 5];
        MemoryNetworkStream source = new MemoryNetworkStream(new MemoryStream(sourceData));
        MemoryNetworkStream destination = new MemoryNetworkStream(new MemoryStream());

        await _forwarder.ForwardAsync(source, destination, CancellationToken.None);

        Assert.That(destination.ToArray(), Is.EqualTo(sourceData));
    }

    [Test]
    public async Task ForwardAsync_WhenSourceEmpty_ShouldNotWriteAnything()
    {
        MemoryNetworkStream source = new MemoryNetworkStream(new MemoryStream());
        MemoryNetworkStream destination = new MemoryNetworkStream(new MemoryStream());

        await _forwarder.ForwardAsync(source, destination, CancellationToken.None);

        Assert.That(destination.ToArray(), Is.Empty);
    }

    [Test]
    public async Task ForwardAsync_WhenCancelled_ShouldStopBeforeAllDataTransferred()
    {
        byte[] sourceData = new byte[100000];
        for (int i = 0; i < sourceData.Length; i++)
        {
            sourceData[i] = (byte)(i % 256);
        }

        MemoryNetworkStream source = new MemoryNetworkStream(new MemoryStream(sourceData));
        MemoryNetworkStream destination = new MemoryNetworkStream(new MemoryStream());

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await _forwarder.ForwardAsync(source, destination, cancellationTokenSource.Token);

        Assert.That(destination.ToArray().Length, Is.EqualTo(0));
    }
}