using CalibriCore.Models;
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
        foreach (var device in devices.Take(10))
        {
            Console.WriteLine($"  - {device.Hostname} ({device.IP})");
        }
        if (devices.Count > 10)
        {
            Console.WriteLine($"  ... and {devices.Count - 10} more");
        }
        
        // Example: Using Recipe model (core)
        Console.WriteLine("\nRecipe model available in CalibriCore!");
        var recipe = new Recipe
        {
            Name = "Test Calibration",
            Description = "A test recipe"
        };
        Console.WriteLine($"  Recipe: {recipe.Name}");
        
        Console.WriteLine("\nDone!");
    }
}
