using System.Xml.Serialization;

namespace CalibriCore.Models;

[XmlRoot("Recipe")]
public class Recipe
{
    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("Description")]
    public string Description { get; set; } = string.Empty;

    [XmlArray("RequiredRoles")]
    [XmlArrayItem("Role")]
    public List<string> RequiredRoles { get; set; } = new();

    [XmlArray("Steps")]
    [XmlArrayItem("Step")]
    public List<RecipeStep> Steps { get; set; } = new();
}

public class RecipeStep
{
    [XmlAttribute("type")]
    public StepType Type { get; set; }

    [XmlAttribute("order")]
    public int Order { get; set; }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlElement("DeviceRole")]
    public string? DeviceRole { get; set; }

    [XmlElement("Action")]
    public string? Action { get; set; }

    [XmlElement("Parameters")]
    public StepParameters? Parameters { get; set; }

    [XmlElement("Tolerance")]
    public Tolerance? Tolerance { get; set; }

    [XmlElement("OnFail")]
    public FailAction OnFail { get; set; } = FailAction.Abort;

    [XmlElement("Duration")]
    public int? DurationMs { get; set; }

    [XmlIgnore]
    public StepStatus Status { get; set; } = StepStatus.Pending;

    [XmlIgnore]
    public StepResult? Result { get; set; }
}

public class StepParameters
{
    [XmlElement("Voltage")]
    public double? Voltage { get; set; }

    [XmlElement("Current")]
    public double? Current { get; set; }

    [XmlElement("Range")]
    public double? Range { get; set; }

    [XmlElement("Unit")]
    public string Unit { get; set; } = string.Empty;

    [XmlElement("MeasurementType")]
    public string? MeasurementType { get; set; }

    [XmlElement("Channel")]
    public int? Channel { get; set; }

    [XmlElement("RouteName")]
    public string? RouteName { get; set; }
    
    // For calibration: reference device (Keithley)
    [XmlElement("ReferenceDeviceRole")]
    public string? ReferenceDeviceRole { get; set; }
    
    // For calibration: DUT device (H&H)
    [XmlElement("DUTDeviceRole")]
    public string? DUTDeviceRole { get; set; }
    
    // Shunt resistance for current measurement (Ohms)
    [XmlElement("ShuntResistance")]
    public double? ShuntResistance { get; set; }
    
    // Number of samples to average
    [XmlElement("SampleCount")]
    public int SampleCount { get; set; } = 5;
    
    // Settling time in ms
    [XmlElement("SettlingTimeMs")]
    public int SettlingTimeMs { get; set; } = 1000;

    // For CompareDevices step: first device to measure
    [XmlElement("DeviceRole1")]
    public string? DeviceRole1 { get; set; }

    // For CompareDevices step: second device to measure
    [XmlElement("DeviceRole2")]
    public string? DeviceRole2 { get; set; }

    // For CompareDevices step: how to compare (AbsoluteDifference, PercentDifference, Ratio)
    [XmlElement("ComparisonType")]
    public ComparisonType ComparisonType { get; set; } = ComparisonType.AbsoluteDifference;

    // For CompareDevices step: custom parameters for device 1
    [XmlElement("Role1Parameters")]
    public DeviceParameters? Role1Parameters { get; set; }

    // For CompareDevices step: custom parameters for device 2
    [XmlElement("Role2Parameters")]
    public DeviceParameters? Role2Parameters { get; set; }
}

/// <summary>
/// Device-specific parameters for CompareDevices step
/// </summary>
public class DeviceParameters
{
    [XmlElement("Range")]
    public double? Range { get; set; }

    [XmlElement("Unit")]
    public string Unit { get; set; } = string.Empty;

    [XmlElement("MeasurementType")]
    public string? MeasurementType { get; set; }

    [XmlElement("Channel")]
    public int? Channel { get; set; }

    [XmlElement("ShuntResistance")]
    public double? ShuntResistance { get; set; }

    [XmlElement("RouteName")]
    public string? RouteName { get; set; }
}

public class Tolerance
{
    [XmlElement("Nominal")]
    public double Nominal { get; set; }

    [XmlElement("ErrorPercent")]
    public double? ErrorPercent { get; set; }

    [XmlElement("ErrorAbsolute")]
    public double? ErrorAbsolute { get; set; }

    [XmlElement("LowerLimit")]
    public double? LowerLimit { get; set; }

    [XmlElement("UpperLimit")]
    public double? UpperLimit { get; set; }

    public (double Lower, double Upper) GetLimits()
    {
        if (LowerLimit.HasValue && UpperLimit.HasValue)
        {
            return (LowerLimit.Value, UpperLimit.Value);
        }

        if (ErrorPercent.HasValue)
        {
            var delta = Math.Abs(Nominal * ErrorPercent.Value / 100.0);
            return (Nominal - delta, Nominal + delta);
        }

        if (ErrorAbsolute.HasValue)
        {
            return (Nominal - ErrorAbsolute.Value, Nominal + ErrorAbsolute.Value);
        }

        // Default: no tolerance (exact match required)
        return (Nominal, Nominal);
    }

    public bool IsWithinTolerance(double value)
    {
        var (lower, upper) = GetLimits();
        return value >= lower && value <= upper;
    }
}

public class StepResult
{
    public bool Passed { get; set; }
    public double? MeasuredValue { get; set; }
    public string? Unit { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? ErrorMessage { get; set; }
    public double? Deviation { get; set; }

    // For calibration: reference measurement (Keithley) - "Richtiger Wert"
    public double? ReferenceValue { get; set; }

    // For calibration: DUT readback (H&H) - "Anzeige"
    public double? DUTValue { get; set; }

    // Measurement uncertainty (per ISO 17025)
    public double? Uncertainty { get; set; }

    // Warning if >70% of tolerance
    public bool IsWarning { get; set; }

    // For CompareDevices step: first device value
    public double? Device1Value { get; set; }

    // For CompareDevices step: second device value
    public double? Device2Value { get; set; }

    // For CompareDevices step: calculated comparison result
    public double? ComparisonResult { get; set; }
}

public enum StepType
{
    Configure,
    Measurement,
    Wait,
    Switch,
    Message,
    CompareDevices
}

public enum StepStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    Skipped
}

public enum FailAction
{
    Prompt,      // Ask user what to do
    Retry,       // Automatically retry
    Abort,       // Stop the recipe
    Continue,    // Continue to next step
    Shutdown     // Safe shutdown all devices
}

public enum ComparisonType
{
    AbsoluteDifference,  // |Value1 - Value2|
    PercentDifference,   // ((Value1 - Value2) / Value1) * 100
    Ratio                // Value1 / Value2
}
