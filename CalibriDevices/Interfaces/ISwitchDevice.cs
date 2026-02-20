namespace CalibrationDevices.Interfaces;

/// <summary>
/// Interface for switching/relay devices that route signals.
/// </summary>
public interface ISwitchDevice : IDevice
{
    Task SetChannelAsync(int channel);
    Task SetChannelAsync(string routeName);
    Task<int> GetCurrentChannelAsync();
    Task OpenAllChannelsAsync();
    Task<IEnumerable<SwitchRoute>> GetAvailableRoutesAsync();
}

public class SwitchRoute
{
    public string Name { get; set; } = string.Empty;
    public int Channel { get; set; }
    public string Description { get; set; } = string.Empty;
}
