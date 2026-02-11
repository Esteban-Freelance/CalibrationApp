namespace CalibrationApp.Models;

public class CalibrationReport
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string TestBenchId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public CalibrationResult OverallResult { get; set; }
    public List<StepReport> StepReports { get; set; } = new();
    public List<DeviceInfo> Devices { get; set; } = new();
    public string? Comments { get; set; }
}

public class StepReport
{
    public int StepOrder { get; set; }
    public string StepName { get; set; } = string.Empty;
    public StepType StepType { get; set; }
    public StepStatus Status { get; set; }
    public double? NominalValue { get; set; }
    public double? MeasuredValue { get; set; }
    public double? LowerLimit { get; set; }
    public double? UpperLimit { get; set; }
    public double? Deviation { get; set; }
    public string? Unit { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DeviceInfo
{
    public string Role { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? CalibrationDueDate { get; set; }
}

public enum CalibrationResult
{
    NotStarted,
    InProgress,
    Passed,
    Failed,
    Aborted
}
