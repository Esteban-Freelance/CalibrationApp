namespace CalibrationDevices.Interfaces;

/// <summary>
/// Interface for devices that generate output signals (e.g., calibrators, power supplies, signal generators).
/// </summary>
public interface ISourceDevice : IDevice
{
    Task SetOutputAsync(SourceParameters parameters);
    Task EnableOutputAsync();
    Task DisableOutputAsync();
    Task<SourceStatus> GetOutputStatusAsync();
    Task<IEnumerable<SourceCapability>> GetCapabilitiesAsync();
}

public class SourceParameters
{
    public SourceType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? Frequency { get; set; } // For AC sources
    public double? CurrentLimit { get; set; } // For voltage sources
    public double? VoltageLimit { get; set; } // For current sources
}

public class SourceStatus
{
    public bool IsEnabled { get; set; }
    public double ActualValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool InRegulation { get; set; }
    public bool OverloadProtection { get; set; }
}

public class SourceCapability
{
    public SourceType Type { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double Resolution { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public enum SourceType
{
    VoltageDC,
    VoltageAC,
    CurrentDC,
    CurrentAC,
    Resistance,
    Frequency,
    HighVoltage
}
