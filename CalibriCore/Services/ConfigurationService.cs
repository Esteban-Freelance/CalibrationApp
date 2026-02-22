using System.IO;
using System.Xml.Serialization;
using CalibrationDevices.Logging;
using CalibriCore.Models;
using CalibrationDevices.Logging;

namespace CalibriCore.Services;

/// <summary>
/// Service for loading and managing configuration files.
/// </summary>
public class ConfigurationService
{
    private readonly ILogService _log;
    private readonly string _configBasePath;

    public ConfigurationService(string configBasePath, ILogService? logService = null)
    {
        _configBasePath = configBasePath;
        _log = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    public IEnumerable<DeviceConfig> LoadDeviceConfigs()
    {
        var devicesPath = Path.Combine(_configBasePath, "Devices");
        var devices = new List<DeviceConfig>();

        if (!Directory.Exists(devicesPath))
        {
            _log.Warning($"Devices directory not found: {devicesPath}");
            return devices;
        }

        foreach (var file in Directory.GetFiles(devicesPath, "*.xml"))
        {
            try
            {
                var device = LoadXml<DeviceConfig>(file);
                if (device != null)
                {
                    devices.Add(device);
                    _log.Debug($"Loaded device config: {device.Id}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load device config {file}: {ex.Message}");
            }
        }

        _log.Info($"Loaded {devices.Count} device configurations");
        return devices;
    }

    public void SaveDeviceConfig(DeviceConfig device)
    {
        var devicesPath = Path.Combine(_configBasePath, "Devices");
        Directory.CreateDirectory(devicesPath);
        
        var filePath = Path.Combine(devicesPath, $"{device.Id}.xml");
        var serializer = new XmlSerializer(typeof(DeviceConfig));
        
        using var writer = new StreamWriter(filePath);
        serializer.Serialize(writer, device);
        
        _log.Info($"Saved device config: {device.Id}");
    }

    public IEnumerable<TestBenchConfig> LoadTestBenchConfigs()
    {
        var benchesPath = Path.Combine(_configBasePath, "TestBenches");
        var benches = new List<TestBenchConfig>();

        if (!Directory.Exists(benchesPath))
        {
            _log.Warning($"TestBenches directory not found: {benchesPath}");
            return benches;
        }

        foreach (var file in Directory.GetFiles(benchesPath, "*.xml"))
        {
            try
            {
                var bench = LoadXml<TestBenchConfig>(file);
                if (bench != null)
                {
                    benches.Add(bench);
                    _log.Debug($"Loaded test bench config: {bench.Id}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load test bench config {file}: {ex.Message}");
            }
        }

        _log.Info($"Loaded {benches.Count} test bench configurations");
        return benches;
    }

    public IEnumerable<Recipe> LoadRecipes()
    {
        var recipesPath = Path.Combine(_configBasePath, "Recipes");
        var recipes = new List<Recipe>();

        if (!Directory.Exists(recipesPath))
        {
            _log.Warning($"Recipes directory not found: {recipesPath}");
            return recipes;
        }

        foreach (var file in Directory.GetFiles(recipesPath, "*.xml"))
        {
            try
            {
                var recipe = LoadXml<Recipe>(file);
                if (recipe != null)
                {
                    // Sort steps by order
                    recipe.Steps = recipe.Steps.OrderBy(s => s.Order).ToList();
                    recipes.Add(recipe);
                    _log.Debug($"Loaded recipe: {recipe.Id} ({recipe.Steps.Count} steps)");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load recipe {file}: {ex.Message}");
            }
        }

        _log.Info($"Loaded {recipes.Count} recipes");
        return recipes;
    }

    private T? LoadXml<T>(string filePath) where T : class
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StreamReader(filePath);
        return serializer.Deserialize(reader) as T;
    }

    public void SaveReport(CalibrationReport report, string outputPath)
    {
        var serializer = new XmlSerializer(typeof(CalibrationReport));
        var fileName = $"Report_{report.RecipeId}_{report.StartTime:yyyyMMdd_HHmmss}.xml";
        var fullPath = Path.Combine(outputPath, fileName);

        using var writer = new StreamWriter(fullPath);
        serializer.Serialize(writer, report);

        _log.Info($"Report saved to: {fullPath}");
    }
}
