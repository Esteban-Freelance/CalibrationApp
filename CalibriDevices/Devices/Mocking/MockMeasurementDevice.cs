using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;


namespace CalibrationDevices.Devices.Mocking;

/// <summary>
/// Mock implementation of a measurement device (e.g., multimeter).
/// Simulates measurements with configurable noise and delay.
/// </summary>
public class MockMeasurementDevice : IMeasurementDevice
{
    private readonly ILogService _log;
    private readonly Random _random = new();
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private double _lastSourceValue = 0;
    private string _lastSourceUnit = "V";
    
    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "MockMultimeter";
    public DeviceStatus Status => _status;
    
    // Simulation parameters
    public double NoisePercent { get; set; } = 0.05; // 0.05% noise
    public int ResponseDelayMs { get; set; } = 100;
    public double AccuracyPercent { get; set; } = 0.01; // 0.01% accuracy
    
    public MockMeasurementDevice(string id, string name, ILogService? logService = null)
    {
        Id = id;
        Name = name;
        _log = logService;
    }
    
    /// <summary>
    /// Sets the expected source value for simulation purposes.
    /// In a real system, this would come from the actual calibrator.
    /// </summary>
    public void SetExpectedSourceValue(double value, string unit)
    {
        _lastSourceValue = value;
        _lastSourceUnit = unit;
    }
    
    public async Task<bool> ConnectAsync()
    {
        _status = DeviceStatus.Connecting;
        _log.Info($"Connecting to {Name}...", Id);
        
        await Task.Delay(500); // Simulate connection time
        
        _status = DeviceStatus.Connected;
        _log.Info($"Connected to {Name}", Id);
        return true;
    }
    
    public async Task DisconnectAsync()
    {
        _log.Info($"Disconnecting from {Name}...", Id);
        await Task.Delay(100);
        _status = DeviceStatus.Disconnected;
        _log.Info($"Disconnected from {Name}", Id);
    }
    
    public async Task ResetAsync()
    {
        _log.Info($"Resetting {Name}...", Id);
        await Task.Delay(200);
        _log.Info($"{Name} reset complete", Id);
    }
    
    public Task<string> GetIdentificationAsync()
    {
        return Task.FromResult($"MOCK,{DeviceType},{Id},1.0");
    }
    
    public async Task<MeasurementResult> MeasureAsync(MeasurementParameters parameters)
    {
        if (_status != DeviceStatus.Connected)
        {
            return new MeasurementResult
            {
                IsValid = false,
                ErrorMessage = "Device not connected"
            };
        }
        
        _status = DeviceStatus.Busy;
        _log.Debug($"Measuring {parameters.Type} on range {parameters.Range} {parameters.Unit}", Id);
        
        // Simulate settling time
        await Task.Delay(parameters.SettlingTime);
        
        // Simulate measurement with noise
        double baseValue = _lastSourceValue;
        
        // Add systematic error (accuracy)
        double systematicError = baseValue * (AccuracyPercent / 100.0) * (_random.NextDouble() - 0.5) * 2;
        
        // Add random noise
        double noise = baseValue * (NoisePercent / 100.0) * (_random.NextDouble() - 0.5) * 2;
        
        double measuredValue = baseValue + systematicError + noise;
        
        // Simulate response delay
        await Task.Delay(ResponseDelayMs);
        
        _status = DeviceStatus.Connected;
        
        _log.Info($"Measured: {measuredValue:F6} {parameters.Unit}", Id);
        
        return new MeasurementResult
        {
            Value = measuredValue,
            Unit = parameters.Unit,
            Timestamp = DateTime.Now,
            IsValid = true
        };
    }
    
    public Task<IEnumerable<MeasurementRange>> GetAvailableRangesAsync(MeasurementType type)
    {
        var ranges = type switch
        {
            MeasurementType.VoltageDC => new[]
            {
                new MeasurementRange { Name = "200mV", MinValue = -0.2, MaxValue = 0.2, Resolution = 0.0001 },
                new MeasurementRange { Name = "2V", MinValue = -2, MaxValue = 2, Resolution = 0.001 },
                new MeasurementRange { Name = "20V", MinValue = -20, MaxValue = 20, Resolution = 0.01 },
                new MeasurementRange { Name = "200V", MinValue = -200, MaxValue = 200, Resolution = 0.1 },
                new MeasurementRange { Name = "1000V", MinValue = -1000, MaxValue = 1000, Resolution = 1 }
            },
            MeasurementType.CurrentDC => new[]
            {
                new MeasurementRange { Name = "200µA", MinValue = -0.0002, MaxValue = 0.0002, Resolution = 0.0000001 },
                new MeasurementRange { Name = "2mA", MinValue = -0.002, MaxValue = 0.002, Resolution = 0.000001 },
                new MeasurementRange { Name = "20mA", MinValue = -0.02, MaxValue = 0.02, Resolution = 0.00001 },
                new MeasurementRange { Name = "200mA", MinValue = -0.2, MaxValue = 0.2, Resolution = 0.0001 },
                new MeasurementRange { Name = "10A", MinValue = -10, MaxValue = 10, Resolution = 0.01 }
            },
            MeasurementType.Resistance => new[]
            {
                new MeasurementRange { Name = "200Ω", MinValue = 0, MaxValue = 200, Resolution = 0.01 },
                new MeasurementRange { Name = "2kΩ", MinValue = 0, MaxValue = 2000, Resolution = 0.1 },
                new MeasurementRange { Name = "20kΩ", MinValue = 0, MaxValue = 20000, Resolution = 1 },
                new MeasurementRange { Name = "200kΩ", MinValue = 0, MaxValue = 200000, Resolution = 10 },
                new MeasurementRange { Name = "2MΩ", MinValue = 0, MaxValue = 2000000, Resolution = 100 }
            },
            _ => Array.Empty<MeasurementRange>()
        };
        
        return Task.FromResult<IEnumerable<MeasurementRange>>(ranges);
    }
    
    public Task SetRangeAsync(MeasurementType type, MeasurementRange range)
    {
        _log.Debug($"Range set to {range.Name} for {type}", Id);
        return Task.CompletedTask;
    }
}
