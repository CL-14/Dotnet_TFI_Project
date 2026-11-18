using System.Net;
using TfiBus.Core;

namespace TfiBus.Tests;

public class HealthcheckPingerTests
{
    // A stand-in for the network. It counts requests and either answers or throws.
    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly bool _fail;
        public int Calls { get; private set; }

        public FakeHandler(HttpStatusCode status, bool fail)
        {
            _status = status;
            _fail = fail;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (_fail) { throw new HttpRequestException("network down"); }
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    // The tests are async, so they return Task instead of void
    [Fact]
    public async Task PingAsync_ReturnsTrue_WhenTheServiceAcceptsThePing()
    {
        FakeHandler handler = new FakeHandler(HttpStatusCode.OK, false);
        HealthcheckPinger pinger = new HealthcheckPinger(new HttpClient(handler), "https://example.test/ping");

        Assert.True(await pinger.PingAsync());
        Assert.Equal(1, handler.Calls);
    }

    // The ping is not accepted, but the method returns false instead of throwing.
    [Fact]
    public async Task PingAsync_ReturnsFalse_WithoutThrowing_WhenTheNetworkFails()
    {
        FakeHandler handler = new FakeHandler(HttpStatusCode.OK, true);
        HealthcheckPinger pinger = new HealthcheckPinger(new HttpClient(handler), "https://example.test/ping");

        Assert.False(await pinger.PingAsync());
    }

    // The ping is not accepted, but the method returns false instead of throwing.
    [Fact]
    public async Task PingAsync_DoesNothing_WhenNoUrlIsConfigured()
    {
        FakeHandler handler = new FakeHandler(HttpStatusCode.OK, false);
        HealthcheckPinger pinger = new HealthcheckPinger(new HttpClient(handler), null);

        Assert.False(await pinger.PingAsync());
        Assert.Equal(0, handler.Calls);   // no request was even attempted
    }
}
