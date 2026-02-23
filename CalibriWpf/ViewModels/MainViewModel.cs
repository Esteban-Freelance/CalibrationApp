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
    private readonly ReportService _reportService;
    
    // Event to notify UI when report is saved
    public event EventHandler<string>? OnReportSaved;
    
    // Event to notify UI when editing device config
    public event EventHandler<DeviceViewModel>? OnDeviceConfigEdit;
    
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
        var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "Reports");
        
        _configService = new ConfigurationService(configPath, _log);
        _reportService = new ReportService(cachePath, _log);
        _testBench = new TestBenchService(_log);
        _recipeRunner = new RecipeRunner(_testBench, _log, _reportService);
        
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
            // Get device config for address/port
            var deviceConfig = _deviceConfigs.FirstOrDefault(d => 
                d.Name.Equals(device.Name, StringComparison.OrdinalIgnoreCase));
            
            Devices.Add(new DeviceViewModel
            {
                Role = role,
                Name = device.Name,
                DeviceType = device.DeviceType,
                Status = device.Status.ToString(),
                IsConnected = false,
                Address = deviceConfig?.Connection?.Address ?? "",
                Port = int.TryParse(deviceConfig?.Connection?.Port, out var p) ? p : 0,
                IsConnecting = true
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
                deviceVm.ConnectionFailed = !deviceVm.IsConnected;
            }
            deviceVm.IsConnecting = false;
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
    
    [RelayCommand]
    private async Task ReconnectDeviceAsync(DeviceViewModel deviceVm)
    {
        if (string.IsNullOrEmpty(deviceVm.Role)) return;
        
        _log.Info($"Reconnecting device: {deviceVm.Role}");
        
        // Reset states
        deviceVm.IsConnecting = true;
        deviceVm.ConnectionFailed = false;
        
        var device = _testBench.GetDeviceByRole(deviceVm.Role);
        if (device != null)
        {
            try
            {
                await device.DisconnectAsync();
                await device.ConnectAsync();
                deviceVm.IsConnected = device.Status == DeviceStatus.Connected;
                deviceVm.Status = device.Status.ToString();
                _log.Info($"Device {deviceVm.Role} reconnected: {deviceVm.IsConnected}");
                
                if (!deviceVm.IsConnected)
                {
                    deviceVm.ConnectionFailed = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to reconnect {deviceVm.Role}: {ex.Message}");
                deviceVm.Status = "Error";
                deviceVm.IsConnected = false;
                deviceVm.ConnectionFailed = true;
            }
        }
        
        deviceVm.IsConnecting = false;
        UpdateCanRun();
    }
    
    [RelayCommand]
    private void EditDeviceConfig(DeviceViewModel deviceVm)
    {
        OnDeviceConfigEdit?.Invoke(this, deviceVm);
    }
    
    public void SaveDeviceConfig(string role, string address, int port)
    {
        var deviceConfig = _deviceConfigs.FirstOrDefault(d => 
            d.Name.Equals(Devices.FirstOrDefault(dv => dv.Role == role)?.Name, StringComparison.OrdinalIgnoreCase));
        
        if (deviceConfig != null)
        {
            if (deviceConfig.Connection == null)
            {
                deviceConfig.Connection = new ConnectionConfig();
            }
            
            deviceConfig.Connection.Address = address;
            deviceConfig.Connection.Port = port.ToString();
            
            _configService.SaveDeviceConfig(deviceConfig);
            _log.Info($"Saved device config for {role}: {address}:{port}");
        }
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
            
            // Create folder for this report
            var reportsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
            var reportFolderName = $"Report_{report.RecipeId}_{report.StartTime:yyyyMMdd_HHmmss}";
            var reportFolder = Path.Combine(reportsPath, reportFolderName);
            Directory.CreateDirectory(reportFolder);
            
            // Save in multiple formats
            var xmlPath = Path.Combine(reportFolder, "report.xml");
            var jsonPath = Path.Combine(reportFolder, "report.json");
            var csvPath = Path.Combine(reportFolder, "report.csv");
            
            _reportService.ExportToXml(report, xmlPath);
            _reportService.ExportToJson(report, jsonPath);
            _reportService.ExportToCsv(report, csvPath);
            
            // Also save to cache
            _reportService.SaveToCache(report);
            
            // Notify UI to show dialog
            LastReportPath = reportFolder;
            OnReportSaved?.Invoke(this, reportFolder);
            
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
    
    [ObservableProperty]
    private bool _isConnecting;
    
    [ObservableProperty]
    private bool _connectionFailed;
    
    [ObservableProperty]
    private string _address = "";
    
    [ObservableProperty]
    private int _port;
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
