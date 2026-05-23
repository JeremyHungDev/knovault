using System.Net;
using System.Net.Sockets;

namespace Knovault.Api.Hosting;

public static class NetworkPorts
{
    /// <summary>偏好埠可用就用它，否則回傳一個系統指派的空閒埠。</summary>
    public static int FindFreePort(int preferred)
    {
        if (IsFree(preferred)) return preferred;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
