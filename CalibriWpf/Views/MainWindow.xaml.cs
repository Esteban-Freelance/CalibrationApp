using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Specialized;

namespace CalibrationApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Ensure window is centered on screen
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        
        Loaded += OnLoaded;
        
        // Auto-scroll log to bottom
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.OnReportSaved += OnReportSaved;
            vm.OnDeviceConfigEdit += OnDeviceConfigEdit;
        }
    }
    
    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            LogScrollViewer?.ScrollToEnd();
        }
    }
    
    private void OnReportSaved(object? sender, string reportPath)
    {
        // Create a styled dialog
        var dialog = new Window
        {
            Title = "Report Saved",
            Width = 500,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")),
            Padding = new Thickness(24)
        };
        
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        // Success message
        var message = new TextBlock
        {
            Text = "✓ Calibration Report Saved",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        
        // Path
        var path = new TextBlock
        {
            Text = reportPath,
            FontSize = 11,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 450,
            Margin = new Thickness(0, 0, 0, 20)
        };
        
        // OK Button
        var button = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
            Foreground = Brushes.White,
            Padding = new Thickness(16, 8, 16, 8),
            BorderThickness = new Thickness(0)
        };
        
        button.Click += (s, e) => dialog.Close();
        
        panel.Children.Add(message);
        panel.Children.Add(path);
        panel.Children.Add(button);
        
        dialog.Content = panel;
        
        dialog.ShowDialog();
    }
    
    private void OnDeviceConfigEdit(object? sender, ViewModels.DeviceViewModel deviceVm)
    {
        var vm = DataContext as ViewModels.MainViewModel;
        
        // Create a styled dialog for editing device config
        var dialog = new Window
        {
            Title = $"Edit {deviceVm.Role} Config",
            Width = 400,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F4F6")),
            Padding = new Thickness(24)
        };
        
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        
        // Address field
        var addressLabel = new TextBlock { Text = "Address (IP/Hostname):", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        var addressBox = new TextBox { Text = deviceVm.Address };

        // Port field
        var portLabel = new TextBlock { Text = "Port:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };
        var portBox = new TextBox { Text = deviceVm.Port.ToString(), Width = 100 };
        portBox.PreviewTextInput += (s, e) => e.Handled = !int.TryParse(e.Text, out _);

        // Buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
        
        var saveButton = new Button { Content = "Save", Width = 80, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")), Foreground = Brushes.White, Padding = new Thickness(16, 8, 16, 8), BorderThickness = new Thickness(0) };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")), Foreground = Brushes.White, Padding = new Thickness(16, 8, 16, 8), BorderThickness = new Thickness(0), Margin = new Thickness(8, 0, 0, 0) };
        
        saveButton.Click += (s, e) =>
        {
            var newAddress = addressBox.Text ?? "";
            int newPort = int.TryParse(portBox.Text, out var p) ? Math.Clamp(p, 1, 65535) : 5025;
            
            deviceVm.Address = newAddress;
            deviceVm.Port = newPort;
            
            // Save to XML file
            vm?.SaveDeviceConfig(deviceVm.Role, newAddress, newPort);
            
            dialog.Close();
        };
        
        cancelButton.Click += (s, e) => dialog.Close();
        
        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        
        panel.Children.Add(addressLabel);
        panel.Children.Add(addressBox);
        panel.Children.Add(portLabel);
        panel.Children.Add(portBox);
        panel.Children.Add(buttonPanel);
        
        dialog.Content = panel;
        
        dialog.ShowDialog();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }
}
