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

    [XmlElement("Resistance")]
    public double? Resistance { get; set; }

    [XmlElement("Frequency")]
    public double? Frequency { get; set; }

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
}

public enum StepType
{
    Configure,
    Measurement,
    Wait,
    Switch,
    Message
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
