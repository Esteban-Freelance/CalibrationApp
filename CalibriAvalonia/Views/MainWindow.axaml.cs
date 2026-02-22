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
}
