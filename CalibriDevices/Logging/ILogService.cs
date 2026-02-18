using System.Collections.ObjectModel;

namespace CalibrationDevices.Logging;

public interface ILogService
{
    ObservableCollection<LogEntry> LogEntries { get; }
    void Log(LogLevel level, string message, string? source = null);
    void Info(string message, string? source = null);
    void Warning(string message, string? source = null);
    void Error(string message, string? source = null);
    void Debug(string message, string? source = null);
    void Clear();
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    
    public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {(Source != null ? $"[{Source}] " : "")}{Message}";
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class NullLogService : ILogService
{
    public static NullLogService Instance { get; } = new();
    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public void Log(LogLevel level, string message, string? source = null) { }
    public void Info(string message, string? source = null) { }
    public void Warning(string message, string? source = null) { }
    public void Error(string message, string? source = null) { }
    public void Debug(string message, string? source = null) { }
    public void Clear() { }
}
