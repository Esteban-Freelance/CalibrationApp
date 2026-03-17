using CalibrationDevices.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalibrationApp.ViewModels
{
    public class WpfLogService : ILogService
    {
        private static readonly Lazy<WpfLogService> _instance = new(() => new WpfLogService());
        public static WpfLogService Instance => _instance.Value;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public void Debug(string message, string? source = null) => Log(LogLevel.Debug, message, source);
        public void Info(string message, string? source = null) => Log(LogLevel.Info, message, source);
        public void Warning(string message, string? source = null) => Log(LogLevel.Warning, message, source);
        public void Error(string message, string? source = null) => Log(LogLevel.Error, message, source);
        public void Clear() => System.Windows.Application.Current?.Dispatcher.Invoke(() => LogEntries.Clear());

        public void Log(LogLevel level, string message, string? source = null)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                LogEntries.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message,
                    Source = source
                });
            });
        }
    }
}
