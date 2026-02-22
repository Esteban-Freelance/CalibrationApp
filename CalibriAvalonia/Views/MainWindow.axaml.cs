using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CalibriAvalonia.ViewModels;
using System;

namespace CalibriAvalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OnReportSaved += OnReportSaved;
            vm.OnDeviceConfigEdit += OnDeviceConfigEdit;
        }
    }
    
    private async void OnReportSaved(object? sender, string reportPath)
    {
        // Create a styled dialog
        var dialog = new Window
        {
            Title = "Report Saved",
            Width = 500,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#F3F4F6")),
            Padding = new Thickness(24)
        };
        
        var panel = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        // Success icon
        var icon = new TextBlock
        {
            Text = "✓",
            FontSize = 48,
            Foreground = new SolidColorBrush(Color.Parse("#10B981")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        
        // Message
        var message = new TextBlock
        {
            Text = "Calibration Report Saved",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        
        // Path
        var path = new TextBlock
        {
            Text = reportPath,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#6B7280")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 450
        };
        
        // OK Button
        var button = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#3B82F6")),
            Foreground = new SolidColorBrush(Colors.White),
            Padding = new Thickness(16, 8)
        };
        
        button.Click += (s, e) => dialog.Close();
        
        panel.Children.Add(icon);
        panel.Children.Add(message);
        panel.Children.Add(path);
        panel.Children.Add(button);
        
        dialog.Content = panel;
        
        // Show as modal dialog
        await dialog.ShowDialog(this);
    }
    
    private async void OnDeviceConfigEdit(object? sender, DeviceViewModel deviceVm)
    {
        // Create a styled dialog for editing device config
        var dialog = new Window
        {
            Title = $"Edit {deviceVm.Role} Config",
            Width = 400,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#F3F4F6")),
            Padding = new Thickness(24)
        };
        
        var panel = new StackPanel { Spacing = 12 };
        
        // Address field
        var addressLabel = new TextBlock { Text = "Address (IP/Hostname):", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        var addressBox = new TextBox { Text = deviceVm.Address, Watermark = "192.168.1.100" };
        
        // Port field
        var portLabel = new TextBlock { Text = "Port:", FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 4) };
        var portBox = new NumericUpDown { Value = deviceVm.Port, Minimum = 1, Maximum = 65535, Increment = 1 };
        
        // Buttons
        var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        
        var saveButton = new Button { Content = "Save", Width = 80, Background = new SolidColorBrush(Color.Parse("#10B981")), Foreground = new SolidColorBrush(Colors.White), Padding = new Thickness(16, 8) };
        var cancelButton = new Button { Content = "Cancel", Width = 80, Background = new SolidColorBrush(Color.Parse("#6B7280")), Foreground = new SolidColorBrush(Colors.White), Padding = new Thickness(16, 8) };
        
        saveButton.Click += (s, e) =>
        {
            deviceVm.Address = addressBox.Text ?? "";
            deviceVm.Port = (int)(portBox.Value ?? 0);
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
        
        await dialog.ShowDialog(this);
    }
}
