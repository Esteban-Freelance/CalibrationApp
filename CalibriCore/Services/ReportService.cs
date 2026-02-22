using System.IO;
using System.Text.Json;
using System.Xml.Serialization;
using CalibriCore.Models;
using CalibrationDevices.Logging;

namespace CalibriCore.Services;

/// <summary>
/// Service for managing calibration reports - caching and export.
/// </summary>
public class ReportService
{
    private readonly string _cachePath;
    private readonly ILogService _log;

    public ReportService(string cachePath, ILogService? logService = null)
    {
        _cachePath = cachePath;
        _log = logService ?? NullLogService.Instance;
        
        if (!Directory.Exists(_cachePath))
        {
            Directory.CreateDirectory(_cachePath);
        }
    }

    /// <summary>
    /// Save report to cache (JSON format).
    /// </summary>
    public void SaveToCache(CalibrationReport report)
    {
        try
        {
            var fileName = $"calibration_{report.ReportId}.json";
            var filePath = Path.Combine(_cachePath, fileName);
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            
            var json = JsonSerializer.Serialize(report, options);
            File.WriteAllText(filePath, json);
            
            _log.Debug($"Report cached: {filePath}");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to cache report: {ex.Message}");
        }
    }

    /// <summary>
    /// Export report to JSON file.
    /// </summary>
    public void ExportToJson(CalibrationReport report, string outputPath)
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };
        
        var json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(outputPath, json);
        
        _log.Info($"Report exported to JSON: {outputPath}");
    }

    /// <summary>
    /// Export report to XML file.
    /// </summary>
    public void ExportToXml(CalibrationReport report, string outputPath)
    {
        var serializer = new XmlSerializer(typeof(CalibrationReport));
        using var writer = new StreamWriter(outputPath);
        serializer.Serialize(writer, report);
        
        _log.Info($"Report exported to XML: {outputPath}");
    }

    /// <summary>
    /// Export report to CSV (simple format for spreadsheets).
    /// </summary>
    public void ExportToCsv(CalibrationReport report, string outputPath)
    {
        var lines = new List<string>
        {
            "Step;Name;Type;Status;Nominal;Measured;Unit;Deviation;LowerLimit;UpperLimit;Error"
        };

        foreach (var step in report.StepReports)
        {
            lines.Add($"{step.StepOrder};{EscapeCsv(step.StepName)};{step.StepType};{step.Status};" +
                     $"{step.NominalValue?.ToString() ?? ""};" +
                     $"{step.MeasuredValue?.ToString() ?? ""};" +
                     $"{step.Unit ?? ""};" +
                     $"{step.Deviation?.ToString() ?? ""};" +
                     $"{step.LowerLimit?.ToString() ?? ""};" +
                     $"{step.UpperLimit?.ToString() ?? ""};" +
                     $"{EscapeCsv(step.ErrorMessage ?? "")}");
        }

        File.WriteAllLines(outputPath, lines);
        _log.Info($"Report exported to CSV: {outputPath}");
    }

    /// <summary>
    /// Get list of cached reports.
    /// </summary>
    public IEnumerable<(string ReportId, DateTime Timestamp)> GetCachedReports()
    {
        if (!Directory.Exists(_cachePath))
            yield break;

        foreach (var file in Directory.GetFiles(_cachePath, "calibration_*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var id = name.Replace("calibration_", "");
            
            var info = new FileInfo(file);
            yield return (id, info.CreationTime);
        }
    }

    /// <summary>
    /// Load a cached report by ID.
    /// </summary>
    public CalibrationReport? LoadFromCache(string reportId)
    {
        var filePath = Path.Combine(_cachePath, $"calibration_{reportId}.json");
        
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<CalibrationReport>(json);
    }

    /// <summary>
    /// Delete a cached report.
    /// </summary>
    public void DeleteFromCache(string reportId)
    {
        var filePath = Path.Combine(_cachePath, $"calibration_{reportId}.json");
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _log.Debug($"Deleted cached report: {reportId}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
