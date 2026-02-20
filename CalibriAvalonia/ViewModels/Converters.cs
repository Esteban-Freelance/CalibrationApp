using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CalibrationDevices.Logging;

namespace CalibriAvalonia.ViewModels;

public class BoolToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrent && isCurrent)
            return new SolidColorBrush(Color.Parse("#EFF6FF"));
        return Brushes.White;
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToBorderBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isCurrent && isCurrent)
            return new SolidColorBrush(Color.Parse("#3B82F6"));
        return new SolidColorBrush(Color.Parse("#E5E7EB"));
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Running" => new SolidColorBrush(Color.Parse("#3B82F6")),
            "Passed" => new SolidColorBrush(Color.Parse("#22C55E")),
            "Failed" => new SolidColorBrush(Color.Parse("#EF4444")),
            _ => new SolidColorBrush(Color.Parse("#E5E7EB"))
        };
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToBadgeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Running" => new SolidColorBrush(Color.Parse("#DBEAFE")),
            "Passed" => new SolidColorBrush(Color.Parse("#DCFCE7")),
            "Failed" => new SolidColorBrush(Color.Parse("#FEE2E2")),
            _ => new SolidColorBrush(Color.Parse("#F3F4F6"))
        };
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToBadgeForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        return status switch
        {
            "Running" => new SolidColorBrush(Color.Parse("#3B82F6")),
            "Passed" => new SolidColorBrush(Color.Parse("#22C55E")),
            "Failed" => new SolidColorBrush(Color.Parse("#EF4444")),
            _ => new SolidColorBrush(Color.Parse("#6B7280"))
        };
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ResultToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value?.ToString() ?? "";
        return result switch
        {
            "Passed" => new SolidColorBrush(Color.Parse("#DCFCE7")),
            "Failed" => new SolidColorBrush(Color.Parse("#FEE2E2")),
            "InProgress" => new SolidColorBrush(Color.Parse("#DBEAFE")),
            _ => new SolidColorBrush(Color.Parse("#F9FAFB"))
        };
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogLevelToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => new SolidColorBrush(Color.Parse("#EF4444")),
                LogLevel.Warning => new SolidColorBrush(Color.Parse("#F59E0B")),
                LogLevel.Info => new SolidColorBrush(Color.Parse("#E5E7EB")),
                LogLevel.Debug => new SolidColorBrush(Color.Parse("#9CA3AF")),
                _ => new SolidColorBrush(Color.Parse("#E5E7EB"))
            };
        }
        return new SolidColorBrush(Color.Parse("#E5E7EB"));
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToConnectionColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isConnected && isConnected)
            return new SolidColorBrush(Color.Parse("#22C55E")); // Green
        return new SolidColorBrush(Color.Parse("#9CA3AF")); // Gray
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
