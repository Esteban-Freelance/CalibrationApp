using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public class NetworkScanner
{
    public class ScanResult
    {
        public string IP { get; set; }
        public string Hostname { get; set; }
        public string Subnet { get; set; } // e.g. "192.168.178"
    }

    public async Task<List<ScanResult>> ScanLocalSubnetAsync(
        int timeout = 200,
        int maxParallel = 50,
        CancellationToken cancellationToken = default)
    {
        var subnets = GetAllLocalSubnets();          // e.g. ["192.168.178", "10.0.0"]
        if (subnets.Count == 0)
            return new List<ScanResult>();

        var semaphore = new SemaphoreSlim(maxParallel);
        var tasks = new List<Task>();
        var results = new List<ScanResult>();

        // Build unique IPs across all detected /24 subnets
        var ipsToScan = new HashSet<(string ip, string subnet)>();
        foreach (var subnet in subnets)
        {
            for (int i = 1; i <= 254; i++)
                ipsToScan.Add(($"{subnet}.{i}", subnet));
        }

        foreach (var item in ipsToScan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(item.ip, timeout);

                    if (reply.Status == IPStatus.Success)
                    {
                        string hostname = null;
                        try
                        {
                            hostname = (await Dns.GetHostEntryAsync(item.ip)).HostName;
                        }
                        catch
                        {
                            // leave hostname null if not resolvable
                        }

                        lock (results)
                        {
                            results.Add(new ScanResult
                            {
                                IP = item.ip,
                                Hostname = hostname,
                                Subnet = item.subnet
                            });
                        }
                    }
                }
                catch
                {
                    // ignore ping/DNS exceptions
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        // Optional: stable ordering
        return results
            .OrderBy(r => r.Subnet)
            .ThenBy(r => IPAddress.Parse(r.IP).GetAddressBytes(), ByteArrayComparer.Instance)
            .ToList();
    }

    private static List<string> GetAllLocalSubnets()
    {
        var subnets = new HashSet<string>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;

            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            // Optional: ignore tunnel/VPN-like interfaces if you don't want them
            // if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

            var ipProps = ni.GetIPProperties();

            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                // Ignore APIPA (169.254.x.x) if desired
                var bytes = addr.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue;

                var parts = addr.Address.ToString().Split('.');
                if (parts.Length == 4)
                    subnets.Add($"{parts[0]}.{parts[1]}.{parts[2]}"); // /24 assumption
            }
        }

        return subnets.ToList();
    }

    // Helper for sorting IPs numerically (optional)
    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new ByteArrayComparer();
        public int Compare(byte[] x, byte[] y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int len = Math.Min(x.Length, y.Length);
            for (int i = 0; i < len; i++)
            {
                int c = x[i].CompareTo(y[i]);
                if (c != 0) return c;
            }
            return x.Length.CompareTo(y.Length);
        }
    }
}