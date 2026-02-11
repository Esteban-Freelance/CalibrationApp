using CalibrationApp.Interfaces;
using CalibrationApp.Logging;
using CalibrationApp.Models;

namespace CalibrationApp.Devices;

/// <summary>
/// Mock implementation of a high voltage source/generator.
/// </summary>
public class MockHighVoltageSource : ISourceDevice
{
    private readonly ILogService _log;
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private bool _outputEnabled = false;
    private SourceParameters? _currentOutput;
    private MockMeasurementDevice? _connectedMeter;
    
    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "MockHVSource";
    public DeviceStatus Status => _status;
    
    public double MaxVoltage { get; set; } = 10000; // 10kV max
    public int ResponseDelayMs { get; set; } = 200; // Slower for HV safety
    
    public MockHighVoltageSource(string id, string name, ILogService? logService = null)
    {
        Id = id;
        Name = name;
        _log = logService ?? LogService.Instance;
    }
    
    public void ConnectMeter(MockMeasurementDevice meter)
    {
        _connectedMeter = meter;
    }
    
    public async Task<bool> ConnectAsync()
    {
        _status = DeviceStatus.Connecting;
        _log.Info($"Connecting to {Name}...", Id);
        _log.Warning("High Voltage Source - Exercise caution!", Id);
        
        await Task.Delay(1000); // Longer connection for HV equipment
        
        _status = DeviceStatus.Connected;
        _log.Info($"Connected to {Name}", Id);
        return true;
    }
    
    public async Task DisconnectAsync()
    {
        if (_outputEnabled)
        {
            _log.Warning("Disabling HV output before disconnect", Id);
            await DisableOutputAsync();
        }
        
        _log.Info($"Disconnecting from {Name}...", Id);
        await Task.Delay(500);
        _status = DeviceStatus.Disconnected;
        _log.Info($"Disconnected from {Name}", Id);
    }
    
    public async Task ResetAsync()
    {
        _log.Info($"Resetting {Name}...", Id);
        await DisableOutputAsync();
        _currentOutput = null;
        await Task.Delay(500);
        _log.Info($"{Name} reset complete - output at safe level", Id);
    }
    
    public Task<string> GetIdentificationAsync()
    {
        return Task.FromResult($"MOCK,{DeviceType},{Id},1.0,{MaxVoltage}V");
    }
    
    public async Task SetOutputAsync(SourceParameters parameters)
    {
        if (_status != DeviceStatus.Connected)
        {
            throw new InvalidOperationException("Device not connected");
        }
        
        if (parameters.Value > MaxVoltage)
        {
            throw new ArgumentException($"Requested voltage {parameters.Value}V exceeds maximum {MaxVoltage}V");
        }
        
        _status = DeviceStatus.Busy;
        _log.Warning($"Setting HV output: {parameters.Value} {parameters.Unit}", Id);
        
        // Simulate slow ramp-up for safety
        await Task.Delay(ResponseDelayMs + (int)(parameters.Value / 100));
        
        _currentOutput = parameters;
        _status = DeviceStatus.Connected;
        
        _log.Info($"HV output configured to {parameters.Value} {parameters.Unit}", Id);
    }
    
    public async Task EnableOutputAsync()
    {
        if (_currentOutput == null)
        {
            _log.Error("No output configured, cannot enable HV", Id);
            throw new InvalidOperationException("Configure output before enabling");
        }
        
        _log.Warning($"ENABLING HIGH VOLTAGE: {_currentOutput.Value} {_currentOutput.Unit}", Id);
        await Task.Delay(ResponseDelayMs);
        _outputEnabled = true;
        
        _connectedMeter?.SetExpectedSourceValue(_currentOutput.Value, _currentOutput.Unit);
        
        _log.Warning($"HV OUTPUT ACTIVE: {_currentOutput.Value} {_currentOutput.Unit}", Id);
    }
    
    public async Task DisableOutputAsync()
    {
        _log.Info("Disabling HV output - ramping down", Id);
        
        // Simulate controlled discharge
        if (_currentOutput != null && _outputEnabled)
        {
            await Task.Delay((int)(_currentOutput.Value / 50)); // Discharge time
        }
        
        _outputEnabled = false;
        _connectedMeter?.SetExpectedSourceValue(0, "V");
        
        _log.Info("HV output disabled - safe", Id);
    }
    
    public Task<SourceStatus> GetOutputStatusAsync()
    {
        return Task.FromResult(new SourceStatus
        {
            IsEnabled = _outputEnabled,
            ActualValue = _outputEnabled ? (_currentOutput?.Value ?? 0) : 0,
            Unit = _currentOutput?.Unit ?? "V",
            InRegulation = _outputEnabled,
            OverloadProtection = false
        });
    }
    
    public Task<IEnumerable<SourceCapability>> GetCapabilitiesAsync()
    {
        var capabilities = new[]
        {
            new SourceCapability 
            { 
                Type = SourceType.HighVoltage, 
                MinValue = 0, 
                MaxValue = MaxVoltage, 
                Resolution = 1, 
                Unit = "V" 
            }
        };
        
        return Task.FromResult<IEnumerable<SourceCapability>>(capabilities);
    }
}
