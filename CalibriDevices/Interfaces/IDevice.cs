namespace CalibrationDevices.Interfaces;

/// <summary>
/// Base interface for all devices in the calibration system.
/// </summary>
public interface IDevice
{
    string Id { get; }
    string Name { get; }
    string DeviceType { get; }
    DeviceStatus Status { get; }
    
    Task<bool> ConnectAsync();
    Task DisconnectAsync();
    Task ResetAsync();
    Task<string> GetIdentificationAsync();
}

public enum DeviceStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error,
    Busy
}
