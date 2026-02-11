namespace CalibrationApp.Interfaces;

/// <summary>
/// Interface for devices that can measure values (e.g., multimeters, oscilloscopes).
/// </summary>
public interface IMeasurementDevice : IDevice
{
    Task<MeasurementResult> MeasureAsync(MeasurementParameters parameters);
    Task<IEnumerable<MeasurementRange>> GetAvailableRangesAsync(MeasurementType type);
    Task SetRangeAsync(MeasurementType type, MeasurementRange range);
}

public class MeasurementParameters
{
    public MeasurementType Type { get; set; }
    public double Range { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int SampleCount { get; set; } = 1;
    public TimeSpan SettlingTime { get; set; } = TimeSpan.FromMilliseconds(100);
}

public class MeasurementResult
{
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class MeasurementRange
{
    public string Name { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double Resolution { get; set; }
}

public enum MeasurementType
{
    VoltageDC,
    VoltageAC,
    CurrentDC,
    CurrentAC,
    Resistance,
    Frequency,
    Temperature,
    Capacitance
}
