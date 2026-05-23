using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Knovault.Api.Hosting;
using Xunit;

namespace Knovault.Api.Tests;

public class NetworkPortsTests
{
    [Fact]
    public void FindFreePort_returns_preferred_when_free()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var preferred = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        NetworkPorts.FindFreePort(preferred).Should().Be(preferred);
    }

    [Fact]
    public void FindFreePort_returns_alternative_when_preferred_taken()
    {
        var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        var taken = ((IPEndPoint)occupied.LocalEndpoint).Port;
        try
        {
            var result = NetworkPorts.FindFreePort(taken);
            result.Should().NotBe(taken);
            result.Should().BeGreaterThan(0);
        }
        finally { occupied.Stop(); }
    }
}
