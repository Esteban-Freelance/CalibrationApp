using CalibrationDevices.Logging;
using System.Collections.ObjectModel;

namespace CalibrationApp.Logging;

public class LogService : ILogService
{
    private static readonly Lazy<LogService> _instance = new(() => new LogService());
    public static LogService Instance => _instance.Value;
    
    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    
    private readonly object _lock = new();
    
    public void Log(LogLevel level, string message, string? source = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };
        
        // Ensure thread-safe access to the collection
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                LogEntries.Add(entry);
                
                // Keep only last 1000 entries
                while (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveAt(0);
                }
            }
        });
    }
    
    public void Info(string message, string? source = null) => Log(LogLevel.Info, message, source);
    public void Warning(string message, string? source = null) => Log(LogLevel.Warning, message, source);
    public void Error(string message, string? source = null) => Log(LogLevel.Error, message, source);
    public void Debug(string message, string? source = null) => Log(LogLevel.Debug, message, source);
    
    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                LogEntries.Clear();
            }
        });
    }
}
