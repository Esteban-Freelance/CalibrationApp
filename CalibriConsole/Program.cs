using CalibriCore.Services;
using CalibriCore.Models;
using CalibrationDevices.Logging;
using CalibriDevices.Devices;

namespace CalibriConsole;

class Program
{
    static async Task Main(string[] args)
    {
        var scanner = new NetworkScanner();
        var scans = await scanner.ScanLocalSubnetAsync();


        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "run":
                await RunCalibrationAsync(args);
                break;
            case "list":
                await ListConfigsAsync(args);
                break;
            case "scan":
                await ScanNetworkAsync();
                break;
            case "help":
            case "--help":
            case "-h":
                PrintHelp();
                break;
            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                PrintHelp();
                break;
        }
    }

    static void PrintHelp()
    {
        Console.WriteLine("Calibri Console");
        Console.WriteLine("===============");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run          Run a calibration recipe");
        Console.WriteLine("  list         List available configs");
        Console.WriteLine("  scan         Scan network for devices");
        Console.WriteLine("  help         Show this help");
        Console.WriteLine();
        Console.WriteLine("Run options:");
        Console.WriteLine("  --testbench  Test bench name (default: mock_station1)");
        Console.WriteLine("  --recipe     Recipe name (default: HuH-Load-60V-Calibration)");
        Console.WriteLine("  --config     Config path (default: CalibriWpf/Configs)");
        Console.WriteLine("  --operator   Operator name (default: Console)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project CalibriConsole -- run");
        Console.WriteLine("  dotnet run --project CalibriConsole -- run --testbench mock_station1 --recipe HuH-Load-60V-Calibration");
    }

    static async Task RunCalibrationAsync(string[] args)
    {
        var testbench = "mock_station1";
        var recipe = "HuH-Load-60V-Calibration";
        var config = "CalibriWpf/Configs";
        var op = "Console";

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--testbench" && i + 1 < args.Length) testbench = args[++i];
            else if (args[i] == "--recipe" && i + 1 < args.Length) recipe = args[++i];
            else if (args[i] == "--config" && i + 1 < args.Length) config = args[++i];
            else if (args[i] == "--operator" && i + 1 < args.Length) op = args[++i];
        }

        var log = NullLogService.Instance;
        var configPath = Path.GetFullPath(config);

        Console.WriteLine($"Calibration Runner");
        Console.WriteLine($"=================");
        Console.WriteLine($"Config path: {configPath}");
        Console.WriteLine($"Test bench:  {testbench}");
        Console.WriteLine($"Recipe:      {recipe}");
        Console.WriteLine($"Operator:    {op}");
        Console.WriteLine();

        var configService = new ConfigurationService(configPath, log);
        
        Console.WriteLine("Loading configurations...");
        var devices = configService.LoadDeviceConfigs().ToList();
        var benches = configService.LoadTestBenchConfigs().ToList();
        var recipes = configService.LoadRecipes().ToList();

        Console.WriteLine($"  Loaded {devices.Count} devices");
        Console.WriteLine($"  Loaded {benches.Count} test benches");
        Console.WriteLine($"  Loaded {recipes.Count} recipes");

        var benchConfig = benches.FirstOrDefault(b => b.Id.Equals(testbench, StringComparison.OrdinalIgnoreCase));
        if (benchConfig == null)
        {
            Console.WriteLine($"ERROR: Test bench '{testbench}' not found!");
            return;
        }

        var recipeConfig = recipes.FirstOrDefault(r => r.Id.Equals(recipe, StringComparison.OrdinalIgnoreCase));
        if (recipeConfig == null)
        {
            Console.WriteLine($"ERROR: Recipe '{recipe}' not found!");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Test bench: {benchConfig.Name}");
        Console.WriteLine($"Recipe:     {recipeConfig.Name} ({recipeConfig.Steps.Count} steps)");
        Console.WriteLine();

        var testBench = new TestBenchService(log);
        await testBench.InitializeAsync(benchConfig, devices);

        // Create report service for caching and export
        var cachePath = Path.Combine(configPath, "..", "Cache", "Reports");
        var reportService = new ReportService(cachePath, log);

        Console.WriteLine("Connecting devices...");
        await testBench.ConnectAllAsync();

        foreach (var (role, device) in testBench.GetAllDevices())
        {
            Console.WriteLine($"  [{role}] {device.Name} - {device.Status}");
        }
        Console.WriteLine();

        var runner = new RecipeRunner(testBench, log, reportService);
        
        runner.StepCompleted += (s, step) => 
        {
            var status = step.Status == StepStatus.Passed ? "✅" : "❌";
            Console.WriteLine($"  Step {step.Order}: {step.Name} - {status}");
        };
        runner.RecipeCompleted += (s, report) =>
        {
            Console.WriteLine();
            Console.WriteLine("=== CALIBRATION RESULT ===");
            Console.WriteLine($"Overall: {report.OverallResult}");
            Console.WriteLine($"Steps:   {report.StepReports.Count(r => r.Status == StepStatus.Passed)}/{report.StepReports.Count} passed");
            Console.WriteLine($"Report ID: {report.ReportId}");
            
            var failed = report.StepReports.Where(r => r.Status == StepStatus.Failed).ToList();
            if (failed.Any())
            {
                Console.WriteLine("Failed steps:");
                foreach (var f in failed)
                {
                    Console.WriteLine($"  - {f.StepName}: {f.ErrorMessage}");
                }
            }

            // Export reports
            var exportDir = Path.Combine(configPath, "..", "Exports");
            Directory.CreateDirectory(exportDir);

            var baseName = $"Calibration_{report.RecipeId}_{report.StartTime:yyyyMMdd_HHmmss}";
            
            // Export to JSON
            var jsonPath = Path.Combine(exportDir, baseName + ".json");
            reportService.ExportToJson(report, jsonPath);
            Console.WriteLine($"Exported: {jsonPath}");

            // Export to CSV
            var csvPath = Path.Combine(exportDir, baseName + ".csv");
            reportService.ExportToCsv(report, csvPath);
            Console.WriteLine($"Exported: {csvPath}");
        };

        Console.WriteLine("Running calibration...");
        var success = await runner.RunAsync(recipeConfig, op);

        Console.WriteLine();
        Console.WriteLine("Disconnecting devices...");
        await testBench.DisconnectAllAsync();

        Console.WriteLine();
        Console.WriteLine(success ? "✅ CALIBRATION PASSED" : "❌ CALIBRATION FAILED");
    }

    static async Task ListConfigsAsync(string[] args)
    {
        var config = "CalibriWpf/Configs";
        
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length) config = args[++i];
        }

        var log = NullLogService.Instance;
        var configPath = Path.GetFullPath(config);

        var configService = new ConfigurationService(configPath, log);

        Console.WriteLine("Available Configurations");
        Console.WriteLine("=======================");
        Console.WriteLine();

        var devices = configService.LoadDeviceConfigs().ToList();
        var benches = configService.LoadTestBenchConfigs().ToList();
        var recipes = configService.LoadRecipes().ToList();

        Console.WriteLine($"Test Benches ({benches.Count}):");
        foreach (var b in benches)
        {
            Console.WriteLine($"  - {b.Id}: {b.Name} ({b.Devices.Count} devices)");
        }
        Console.WriteLine();

        Console.WriteLine($"Recipes ({recipes.Count}):");
        foreach (var r in recipes)
        {
            Console.WriteLine($"  - {r.Id}: {r.Name} ({r.Steps.Count} steps)");
        }
        Console.WriteLine();

        Console.WriteLine($"Devices ({devices.Count}):");
        foreach (var d in devices)
        {
            Console.WriteLine($"  - {d.Id}: {d.Driver} ({d.Name})");
        }
    }

    static async Task ScanNetworkAsync()
    {
        Console.WriteLine("Scanning local network...");
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
    }
}
