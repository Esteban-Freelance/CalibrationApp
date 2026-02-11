using System.Windows;
using System.Collections.Specialized;

namespace CalibrationApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Auto-scroll log to bottom
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }
    }
    
    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            LogScrollViewer?.ScrollToEnd();
        }
    }
}
