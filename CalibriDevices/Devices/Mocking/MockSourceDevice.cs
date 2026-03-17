using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;


namespace CalibrationDevices.Devices.Mocking;

/// <summary>
/// Mock implementation of a source/calibrator device.
/// Simulates a precision calibrator that can output voltage, current, resistance, etc.
/// </summary>
public class MockSourceDevice : ISourceDevice
{
    private readonly ILogService _log;
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private bool _outputEnabled = false;
    private SourceParameters? _currentOutput;

    // Reference to connected measurement devices for simulation
    private readonly List<MockMeasurementDevice> _connectedMeters = new();

    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "MockCalibrator";
    public DeviceStatus Status => _status;

    public int ResponseDelayMs { get; set; } = 50;

    public MockSourceDevice(string id, string name, ILogService? logService = null)
    {
        Id = id;
        Name = name;
        _log = logService;
    }

    /// <summary>
    /// Connect a measurement device for simulation purposes.
    /// When output is set, the meter will "see" this value.
    /// </summary>
    public void ConnectMeter(MockMeasurementDevice meter)
    {
        _connectedMeters.Add(meter);
    }

    public async Task<bool> ConnectAsync()
    {
        _status = DeviceStatus.Connecting;
        _log.Info($"Connecting to {Name}...", Id);

        await Task.Delay(500);

        _status = DeviceStatus.Connected;
        _log.Info($"Connected to {Name}", Id);
        return true;
    }

    public async Task DisconnectAsync()
    {
        if (_outputEnabled)
        {
            await DisableOutputAsync();
        }

        _log.Info($"Disconnecting from {Name}...", Id);
        await Task.Delay(100);
        _status = DeviceStatus.Disconnected;
        _log.Info($"Disconnected from {Name}", Id);
    }

    public async Task ResetAsync()
    {
        _log.Info($"Resetting {Name}...", Id);
        await DisableOutputAsync();
        _currentOutput = null;
        await Task.Delay(200);
        _log.Info($"{Name} reset complete", Id);
    }

    public Task<string> GetIdentificationAsync()
    {
        return Task.FromResult($"MOCK,{DeviceType},{Id},1.0");
    }

    public async Task SetOutputAsync(SourceParameters parameters)
    {
        if (_status != DeviceStatus.Connected)
        {
            throw new InvalidOperationException("Device not connected");
        }

        _status = DeviceStatus.Busy;
        _log.Info($"Setting output: {parameters.Value} {parameters.Unit} ({parameters.Type})", Id);

        await Task.Delay(ResponseDelayMs);

        _currentOutput = parameters;

        // Update connected meter with the new value
        foreach (var meter in _connectedMeters)
            meter.SetExpectedSourceValue(parameters.Value, parameters.Unit);

        _status = DeviceStatus.Connected;
        _log.Debug($"Output set to {parameters.Value} {parameters.Unit}", Id);
    }

    public async Task EnableOutputAsync()
    {
        if (_currentOutput == null)
        {
            _log.Warning("No output configured, cannot enable", Id);
            return;
        }

        _log.Info("Enabling output", Id);
        await Task.Delay(ResponseDelayMs);
        _outputEnabled = true;

        // Notify the connected meter
        if (_currentOutput != null)
        {
            foreach (var meter in _connectedMeters)
                meter.SetExpectedSourceValue(_currentOutput.Value, _currentOutput.Unit);
        }

        _log.Info($"Output enabled: {_currentOutput?.Value} {_currentOutput?.Unit}", Id);
    }

    public async Task DisableOutputAsync()
    {
        _log.Info("Disabling output", Id);
        await Task.Delay(ResponseDelayMs);
        _outputEnabled = false;

        // Set meter to zero when output disabled
        foreach (var meter in _connectedMeters)
            meter.SetExpectedSourceValue(0, _currentOutput?.Unit ?? "V");

        _log.Info("Output disabled", Id);
    }

    public Task<SourceStatus> GetOutputStatusAsync(SourceParameters parameters)
    {
        return Task.FromResult(new SourceStatus
        {
            IsEnabled = _outputEnabled,
            ActualValue = _currentOutput?.Value ?? 0,
            Unit = _currentOutput?.Unit ?? "",
            InRegulation = _outputEnabled,
            OverloadProtection = false
        });
    }

    public Task<IEnumerable<SourceCapability>> GetCapabilitiesAsync()
    {
        var capabilities = new[]
        {
            // DC Voltage
            new SourceCapability { Type = SourceType.VoltageDC, MinValue = -1100, MaxValue = 1100, Resolution = 0.000001, Unit = "V" },
            // DC Current
            new SourceCapability { Type = SourceType.CurrentDC, MinValue = -11, MaxValue = 11, Resolution = 0.0000001, Unit = "A" },
            // High Voltage
            new SourceCapability { Type = SourceType.HighVoltage, MinValue = 0, MaxValue = 10000, Resolution = 0.001, Unit = "V" }
        };

        return Task.FromResult<IEnumerable<SourceCapability>>(capabilities);
    }
}
