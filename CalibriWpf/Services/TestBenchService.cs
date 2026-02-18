using CalibrationApp.Logging;
using CalibrationApp.Models;
using CalibrationDevices.Devices.Mocking;
using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;

namespace CalibrationApp.Services;

/// <summary>
/// Manages a test bench with connected devices.
/// </summary>
public class TestBenchService
{
    private readonly ILogService _log;
    private readonly Dictionary<string, IDevice> _devices = new();
    private readonly Dictionary<string, string> _roleToDeviceId = new();
    
    public TestBenchConfig? CurrentConfig { get; private set; }
    public bool IsInitialized => CurrentConfig != null && _devices.Any();
    
    public event EventHandler<string>? DeviceStatusChanged;
    
    public TestBenchService(ILogService? logService = null)
    {
        _log = logService ?? LogService.Instance;
    }
    
    public async Task InitializeAsync(TestBenchConfig benchConfig, IEnumerable<DeviceConfig> deviceConfigs)
    {
        _log.Info($"Initializing test bench: {benchConfig.Name}");
        
        CurrentConfig = benchConfig;
        _devices.Clear();
        _roleToDeviceId.Clear();
        
        var deviceDict = deviceConfigs.ToDictionary(d => d.Id);
        
        foreach (var deviceRef in benchConfig.Devices)
        {
            if (!deviceDict.TryGetValue(deviceRef.DeviceId, out var deviceConfig))
            {
                _log.Error($"Device not found: {deviceRef.DeviceId}");
                continue;
            }
            
            try
            {
                var device = DeviceFactory.CreateDevice(deviceConfig, _log);
                _devices[deviceRef.DeviceId] = device;
                _roleToDeviceId[deviceRef.Role] = deviceRef.DeviceId;
                
                _log.Debug($"Created device {device.Name} with role {deviceRef.Role}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to create device {deviceRef.DeviceId}: {ex.Message}");
            }
        }
        
        // Wire up mock devices for simulation
        WireMockDevices();
        
        _log.Info($"Test bench initialized with {_devices.Count} devices");
    }
    
    private void WireMockDevices()
    {
        // Connect calibrators to measurement devices for simulation
        var calibrators = _devices.Values.OfType<MockSourceDevice>().ToList();
        var hvSources = _devices.Values.OfType<MockHighVoltageSource>().ToList();
        var meters = _devices.Values.OfType<MockMeasurementDevice>().ToList();
        
        if (meters.Any())
        {
            var meter = meters.First();
            foreach (var calibrator in calibrators)
            {
                calibrator.ConnectMeter(meter);
                _log.Debug($"Wired {calibrator.Name} to {meter.Name}");
            }
            foreach (var hvSource in hvSources)
            {
                hvSource.ConnectMeter(meter);
                _log.Debug($"Wired {hvSource.Name} to {meter.Name}");
            }
        }
    }
    
    public async Task ConnectAllAsync()
    {
        _log.Info("Connecting all devices...");
        
        foreach (var device in _devices.Values)
        {
            try
            {
                await device.ConnectAsync();
                DeviceStatusChanged?.Invoke(this, device.Id);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to connect {device.Name}: {ex.Message}");
            }
        }
    }
    
    public async Task DisconnectAllAsync()
    {
        _log.Info("Disconnecting all devices...");
        
        foreach (var device in _devices.Values)
        {
            try
            {
                await device.DisconnectAsync();
                DeviceStatusChanged?.Invoke(this, device.Id);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to disconnect {device.Name}: {ex.Message}");
            }
        }
    }
    
    public async Task SafeShutdownAsync()
    {
        _log.Warning("Initiating safe shutdown...");
        
        // First, reset all devices to safe state
        foreach (var device in _devices.Values)
        {
            try
            {
                await device.ResetAsync();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to reset {device.Name}: {ex.Message}");
            }
        }
        
        // Then disconnect
        await DisconnectAllAsync();
        
        _log.Info("Safe shutdown complete");
    }
    
    public IDevice? GetDeviceByRole(string role)
    {
        if (_roleToDeviceId.TryGetValue(role, out var deviceId))
        {
            return _devices.GetValueOrDefault(deviceId);
        }
        return null;
    }
    
    public IDevice? GetDeviceById(string id)
    {
        return _devices.GetValueOrDefault(id);
    }
    
    public IEnumerable<(string Role, IDevice Device)> GetAllDevices()
    {
        foreach (var kvp in _roleToDeviceId)
        {
            if (_devices.TryGetValue(kvp.Value, out var device))
            {
                yield return (kvp.Key, device);
            }
        }
    }
    
    public bool HasRequiredRoles(IEnumerable<string> requiredRoles)
    {
        return requiredRoles.All(role => _roleToDeviceId.ContainsKey(role));
    }
}
