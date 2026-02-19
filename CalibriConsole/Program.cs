using CalibriDevices.Devices;

namespace CalibriConsole;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Calibri Console App");
        Console.WriteLine("===================");
        
        // Example: Scan network
        Console.WriteLine("\nScanning local network...");
        var scanner = new NetworkScanner();
        var devices = await scanner.ScanLocalSubnetAsync();
        
        Console.WriteLine($"Found {devices.Count} device(s):");
        foreach (var device in devices)
        {
            Console.WriteLine($"  - {device.Hostname} ({device.IP})");
        }
        
        Console.WriteLine("\nDone!");
    }
}
