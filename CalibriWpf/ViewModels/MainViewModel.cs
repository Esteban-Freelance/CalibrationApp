using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CalibriCore.Models;
using CalibriCore.Services;
using CalibrationDevices.Interfaces;
using System.IO;
using CalibrationDevices.Logging;

namespace CalibrationApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigurationService _configService;
    private readonly TestBenchService _testBench;
    private readonly RecipeRunner _recipeRunner;
    private readonly ILogService _log;
    
    // Event to notify UI when report is saved
    public event EventHandler<string>? OnReportSaved;
    
    [ObservableProperty]
    private string? _lastReportPath;
    
    [ObservableProperty]
    private ObservableCollection<TestBenchConfig> _testBenches = new();
    
    [ObservableProperty]
    private TestBenchConfig? _selectedTestBench;
    
    [ObservableProperty]
    private ObservableCollection<Recipe> _recipes = new();
    
    [ObservableProperty]
    private Recipe? _selectedRecipe;
    
    [ObservableProperty]
    private ObservableCollection<DeviceViewModel> _devices = new();
    
    [ObservableProperty]
    private ObservableCollection<StepViewModel> _steps = new();
    
    [ObservableProperty]
    private string _currentMeasurement = "—";
    
    [ObservableProperty]
    private string _currentTolerance = "—";
    
    [ObservableProperty]
    private string _currentStatus = "Ready";
    
    [ObservableProperty]
    private bool _isRunning;
    
    [ObservableProperty]
    private bool _isPaused;
    
    [ObservableProperty]
    private bool _canRun;
    
    [ObservableProperty]
    private string _operatorName = "";
    
    [ObservableProperty]
    private CalibrationResult _overallResult = CalibrationResult.NotStarted;
    
    public ObservableCollection<LogEntry> LogEntries => ConsoleLogService.Instance.LogEntries;
    
    private List<DeviceConfig> _deviceConfigs = new();
    
    public MainViewModel()
    {
        _log = ConsoleLogService.Instance;
        
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        _configService = new ConfigurationService(configPath, _log);
        _testBench = new TestBenchService(_log);
        _recipeRunner = new RecipeRunner(_testBench, _log);
        
        // Subscribe to recipe runner events
        _recipeRunner.StepStarted += OnStepStarted;
        _recipeRunner.StepCompleted += OnStepCompleted;
        _recipeRunner.StepFailed += OnStepFailed;
        _recipeRunner.StateChanged += OnStateChanged;
        _recipeRunner.RecipeCompleted += OnRecipeCompleted;
        
        LoadConfigurations();
    }
    
    private void LoadConfigurations()
    {
        _log.Info("Loading configurations...");
        
        _deviceConfigs = _configService.LoadDeviceConfigs().ToList();
        
        TestBenches.Clear();
        foreach (var bench in _configService.LoadTestBenchConfigs())
        {
            TestBenches.Add(bench);
        }
        
        Recipes.Clear();
        foreach (var recipe in _configService.LoadRecipes())
        {
            Recipes.Add(recipe);
        }
        
        _log.Info("Configurations loaded");
    }
    
    partial void OnSelectedTestBenchChanged(TestBenchConfig? value)
    {
        if (value != null)
        {
            _ = InitializeTestBenchAsync(value);
        }
        else
        {
            Devices.Clear();
        }
        UpdateCanRun();
    }
    
    private async Task InitializeTestBenchAsync(TestBenchConfig benchConfig)
    {
        _log.Info($"Selecting test bench: {benchConfig.Name}");
        
        await _testBench.InitializeAsync(benchConfig, _deviceConfigs);
        
        Devices.Clear();
        foreach (var (role, device) in _testBench.GetAllDevices())
        {
            Devices.Add(new DeviceViewModel
            {
                Role = role,
                Name = device.Name,
                DeviceType = device.DeviceType,
                Status = device.Status.ToString(),
                IsConnected = false
            });
        }
        
        // Auto-connect devices
        await _testBench.ConnectAllAsync();
        
        // Update device status in UI
        foreach (var deviceVm in Devices)
        {
            var device = _testBench.GetDeviceByRole(deviceVm.Role);
            if (device != null)
            {
                deviceVm.Status = device.Status.ToString();
                deviceVm.IsConnected = device.Status == DeviceStatus.Connected;
            }
        }
        
        UpdateCanRun();
    }
    
    partial void OnSelectedRecipeChanged(Recipe? value)
    {
        Steps.Clear();
        
        if (value != null)
        {
            foreach (var step in value.Steps)
            {
                step.Status = StepStatus.Pending;
                Steps.Add(new StepViewModel(step));
            }
            _log.Info($"Selected recipe: {value.Name} ({value.Steps.Count} steps)");
        }
        
        UpdateCanRun();
    }
    
    private void UpdateCanRun()
    {
        CanRun = SelectedTestBench != null && 
                 SelectedRecipe != null && 
                 Devices.Any(d => d.IsConnected) &&
                 !IsRunning;
    }
    
    [RelayCommand]
    private async Task RunRecipeAsync()
    {
        if (SelectedRecipe == null) return;
        
        IsRunning = true;
        IsPaused = false;
        OverallResult = CalibrationResult.InProgress;
        UpdateCanRun();
        
        // Reset step statuses
        foreach (var stepVm in Steps)
        {
            stepVm.Status = "Pending";
            stepVm.IsCurrent = false;
        }
        
        await _recipeRunner.RunAsync(SelectedRecipe, OperatorName);
        
        IsRunning = false;
        UpdateCanRun();
    }
    
    [RelayCommand]
    private void PauseRecipe()
    {
        if (IsRunning && !IsPaused)
        {
            _recipeRunner.Pause();
            IsPaused = true;
        }
    }
    
    [RelayCommand]
    private void ResumeRecipe()
    {
        if (IsPaused)
        {
            _recipeRunner.Resume();
            IsPaused = false;
        }
    }
    
    [RelayCommand]
    private void AbortRecipe()
    {
        _recipeRunner.Abort();
    }
    
    [RelayCommand]
    private async Task SafeShutdownAsync()
    {
        await _testBench.SafeShutdownAsync();
        
        foreach (var deviceVm in Devices)
        {
            deviceVm.Status = "Disconnected";
            deviceVm.IsConnected = false;
        }
        
        IsRunning = false;
        IsPaused = false;
        UpdateCanRun();
    }
    
    [RelayCommand]
    private void ClearLog()
    {
        _log.Clear();
    }
    
    [RelayCommand]
    private void RefreshConfigurations()
    {
        LoadConfigurations();
    }
    
    private void OnStepStarted(object? sender, RecipeStep step)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var stepVm = Steps.FirstOrDefault(s => s.Order == step.Order);
            if (stepVm != null)
            {
                stepVm.Status = "Running";
                stepVm.IsCurrent = true;
            }
            
            // Mark previous steps as not current
            foreach (var s in Steps.Where(s => s.Order < step.Order))
            {
                s.IsCurrent = false;
            }
            
            CurrentStatus = $"Running: {step.Name}";
            
            if (step.Tolerance != null)
            {
                var (lower, upper) = step.Tolerance.GetLimits();
                CurrentTolerance = $"{step.Tolerance.Nominal:F4} [{lower:F4} - {upper:F4}]";
            }
            else
            {
                CurrentTolerance = "—";
            }
        });
    }
    
    private void OnStepCompleted(object? sender, RecipeStep step)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var stepVm = Steps.FirstOrDefault(s => s.Order == step.Order);
            if (stepVm != null)
            {
                stepVm.Status = step.Status.ToString();
                stepVm.IsCurrent = false;
                
                if (step.Result?.MeasuredValue.HasValue == true)
                {
                    stepVm.MeasuredValue = $"{step.Result.MeasuredValue:F6} {step.Result.Unit}";
                    CurrentMeasurement = stepVm.MeasuredValue;
                    
                    if (step.Status == StepStatus.Passed)
                    {
                        CurrentStatus = $"✓ {step.Name}: PASS";
                    }
                    else
                    {
                        CurrentStatus = $"✗ {step.Name}: FAIL";
                    }
                }
            }
        });
    }
    
    private void OnStepFailed(object? sender, FailedStepEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentStatus = $"FAILED: {e.Step.Name}";
            
            // For now, default to continue. In a real app, show a dialog.
            // The user would choose: Retry, Abort, Shutdown, or Continue
            e.SelectedAction = FailAction.Continue;
        });
    }
    
    private void OnStateChanged(object? sender, RecipeRunnerState state)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentStatus = state switch
            {
                RecipeRunnerState.Running => "Running...",
                RecipeRunnerState.Paused => "Paused",
                RecipeRunnerState.Completed => "Completed",
                RecipeRunnerState.Aborted => "Aborted",
                RecipeRunnerState.Error => "Error",
                _ => "Ready"
            };
        });
    }
    
    private void OnRecipeCompleted(object? sender, CalibrationReport report)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            OverallResult = report.OverallResult;
            
            var passed = report.StepReports.Count(s => s.Status == StepStatus.Passed);
            var failed = report.StepReports.Count(s => s.Status == StepStatus.Failed);
            
            CurrentStatus = $"Complete: {passed} passed, {failed} failed";
            
            // Save report
            var reportsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            Directory.CreateDirectory(reportsPath);
            _configService.SaveReport(report, reportsPath);
            
            // Log report save location
            var reportFileName = $"Report_{report.RecipeId}_{report.StartTime:yyyyMMdd_HHmmss}.xml";
            var fullReportPath = Path.Combine(reportsPath, reportFileName);
            _log.Debug($"Report saved to: {fullReportPath}");
            
            // Notify UI to show dialog
            LastReportPath = fullReportPath;
            OnReportSaved?.Invoke(this, fullReportPath);
            
            CurrentStatus = $"Complete: {passed} passed, {failed} failed";
        });
    }
}

public partial class DeviceViewModel : ObservableObject
{
    [ObservableProperty]
    private string _role = "";
    
    [ObservableProperty]
    private string _name = "";
    
    [ObservableProperty]
    private string _deviceType = "";
    
    [ObservableProperty]
    private string _status = "";
    
    [ObservableProperty]
    private bool _isConnected;
}

public partial class StepViewModel : ObservableObject
{
    public int Order { get; }
    public string Name { get; }
    public string Type { get; }
    
    [ObservableProperty]
    private string _status = "Pending";
    
    [ObservableProperty]
    private bool _isCurrent;
    
    [ObservableProperty]
    private string _measuredValue = "";
    
    public StepViewModel(RecipeStep step)
    {
        Order = step.Order;
        Name = step.Name;
        Type = step.Type.ToString();
    }
}
