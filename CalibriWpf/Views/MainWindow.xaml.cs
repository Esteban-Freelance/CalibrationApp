using System;
using System.Windows;
using System.Windows.Controls;
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
            Padding = new Thickness(16, 8),
            BorderThickness = new Thickness(0)
        };
        
        button.Click += (s, e) => dialog.Close();
        
        panel.Children.Add(message);
        panel.Children.Add(path);
        panel.Children.Add(button);
        
        dialog.Content = panel;
        
        dialog.ShowDialog(this);
    }
}
