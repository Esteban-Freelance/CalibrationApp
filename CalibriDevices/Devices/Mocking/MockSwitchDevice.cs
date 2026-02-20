using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;


namespace CalibrationDevices.Devices.Mocking;

/// <summary>
/// Mock implementation of a switch/relay matrix device.
/// </summary>
public class MockSwitchDevice : ISwitchDevice
{
    private readonly ILogService _log;
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private int _currentChannel = 0;
    private readonly List<SwitchRoute> _routes;

    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "MockSwitchMatrix";
    public DeviceStatus Status => _status;

    public int ResponseDelayMs { get; set; } = 50;

    public MockSwitchDevice(string id, string name, ILogService? logService = null)
    {
        Id = id;
        Name = name;
        _log = logService;

        // Default routes
        _routes = new List<SwitchRoute>
        {
            new() { Name = "VoltageDC", Channel = 1, Description = "DC Voltage measurement path" },
            new() { Name = "VoltageAC", Channel = 2, Description = "AC Voltage measurement path" },
            new() { Name = "CurrentDC", Channel = 3, Description = "DC Current measurement path" },
            new() { Name = "CurrentAC", Channel = 4, Description = "AC Current measurement path" },
            new() { Name = "Resistance2W", Channel = 5, Description = "2-wire resistance measurement" },
            new() { Name = "Resistance4W", Channel = 6, Description = "4-wire resistance measurement" },
            new() { Name = "HighVoltage", Channel = 7, Description = "High voltage measurement path" },
            new() { Name = "Isolated", Channel = 0, Description = "All paths open" }
        };
    }

    public void ConfigureRoutes(List<SwitchRoute> routes)
    {
        _routes.Clear();
        _routes.AddRange(routes);
    }

    public async Task<bool> ConnectAsync()
    {
        _status = DeviceStatus.Connecting;
        _log.Info($"Connecting to {Name}...", Id);

        await Task.Delay(300);

        _status = DeviceStatus.Connected;
        _log.Info($"Connected to {Name}", Id);

        // Open all channels on connect for safety
        await OpenAllChannelsAsync();

        return true;
    }

    public async Task DisconnectAsync()
    {
        await OpenAllChannelsAsync();

        _log.Info($"Disconnecting from {Name}...", Id);
        await Task.Delay(100);
        _status = DeviceStatus.Disconnected;
        _log.Info($"Disconnected from {Name}", Id);
    }

    public async Task ResetAsync()
    {
        _log.Info($"Resetting {Name}...", Id);
        await OpenAllChannelsAsync();
        _log.Info($"{Name} reset complete", Id);
    }

    public Task<string> GetIdentificationAsync()
    {
        return Task.FromResult($"MOCK,{DeviceType},{Id},1.0");
    }

    public async Task SetChannelAsync(int channel)
    {
        if (_status != DeviceStatus.Connected)
        {
            throw new InvalidOperationException("Device not connected");
        }

        _status = DeviceStatus.Busy;
        _log.Info($"Switching to channel {channel}", Id);

        await Task.Delay(ResponseDelayMs);

        _currentChannel = channel;
        _status = DeviceStatus.Connected;

        var route = _routes.FirstOrDefault(r => r.Channel == channel);
        _log.Debug($"Channel {channel} active ({route?.Name ?? "Unknown"})", Id);
    }

    public async Task SetChannelAsync(string routeName)
    {
        var route = _routes.FirstOrDefault(r =>
            r.Name.Equals(routeName, StringComparison.OrdinalIgnoreCase));

        if (route == null)
        {
            throw new ArgumentException($"Route '{routeName}' not found");
        }

        await SetChannelAsync(route.Channel);
    }

    public Task<int> GetCurrentChannelAsync()
    {
        return Task.FromResult(_currentChannel);
    }

    public async Task OpenAllChannelsAsync()
    {
        _log.Info("Opening all channels", Id);
        await Task.Delay(ResponseDelayMs);
        _currentChannel = 0;
        _log.Debug("All channels open", Id);
    }

    public Task<IEnumerable<SwitchRoute>> GetAvailableRoutesAsync()
    {
        return Task.FromResult<IEnumerable<SwitchRoute>>(_routes);
    }
}
