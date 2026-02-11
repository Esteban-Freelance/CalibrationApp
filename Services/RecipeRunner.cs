using CalibrationApp.Devices;
using CalibrationApp.Interfaces;
using CalibrationApp.Logging;
using CalibrationApp.Models;

namespace CalibrationApp.Services;

/// <summary>
/// Executes calibration recipes step by step.
/// </summary>
public class RecipeRunner
{
    private readonly TestBenchService _testBench;
    private readonly ILogService _log;
    private CancellationTokenSource? _cts;
    private bool _isPaused;
    
    public Recipe? CurrentRecipe { get; private set; }
    public int CurrentStepIndex { get; private set; } = -1;
    public RecipeRunnerState State { get; private set; } = RecipeRunnerState.Idle;
    public CalibrationReport? Report { get; private set; }
    
    public event EventHandler<RecipeStep>? StepStarted;
    public event EventHandler<RecipeStep>? StepCompleted;
    public event EventHandler<FailedStepEventArgs>? StepFailed;
    public event EventHandler<RecipeRunnerState>? StateChanged;
    public event EventHandler<CalibrationReport>? RecipeCompleted;
    
    public RecipeRunner(TestBenchService testBench, ILogService? logService = null)
    {
        _testBench = testBench;
        _log = logService ?? LogService.Instance;
    }
    
    public async Task<bool> RunAsync(Recipe recipe, string operatorName = "")
    {
        if (State == RecipeRunnerState.Running)
        {
            _log.Warning("Recipe already running");
            return false;
        }
        
        // Validate requirements
        if (!_testBench.HasRequiredRoles(recipe.RequiredRoles))
        {
            _log.Error("Test bench missing required device roles");
            return false;
        }
        
        CurrentRecipe = recipe;
        CurrentStepIndex = -1;
        _cts = new CancellationTokenSource();
        _isPaused = false;
        
        // Initialize report
        Report = new CalibrationReport
        {
            StartTime = DateTime.Now,
            RecipeId = recipe.Id,
            RecipeName = recipe.Name,
            TestBenchId = _testBench.CurrentConfig?.Id ?? "",
            OperatorName = operatorName,
            OverallResult = CalibrationResult.InProgress
        };
        
        // Add device info to report
        foreach (var (role, device) in _testBench.GetAllDevices())
        {
            Report.Devices.Add(new DeviceInfo
            {
                Role = role,
                DeviceId = device.Id,
                DeviceName = device.Name,
                DeviceType = device.DeviceType
            });
        }
        
        SetState(RecipeRunnerState.Running);
        _log.Info($"Starting recipe: {recipe.Name}");
        
        try
        {
            // Reset all step statuses
            foreach (var step in recipe.Steps)
            {
                step.Status = StepStatus.Pending;
                step.Result = null;
            }
            
            for (int i = 0; i < recipe.Steps.Count; i++)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    SetState(RecipeRunnerState.Aborted);
                    Report.OverallResult = CalibrationResult.Aborted;
                    break;
                }
                
                // Handle pause
                while (_isPaused && !_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cts.Token);
                }
                
                CurrentStepIndex = i;
                var step = recipe.Steps[i];
                
                var success = await ExecuteStepAsync(step);
                
                if (!success)
                {
                    var action = await HandleFailedStepAsync(step);
                    
                    switch (action)
                    {
                        case FailAction.Retry:
                            i--; // Retry same step
                            continue;
                        case FailAction.Abort:
                            SetState(RecipeRunnerState.Aborted);
                            Report.OverallResult = CalibrationResult.Aborted;
                            Report.EndTime = DateTime.Now;
                            RecipeCompleted?.Invoke(this, Report);
                            return false;
                        case FailAction.Shutdown:
                            await _testBench.SafeShutdownAsync();
                            SetState(RecipeRunnerState.Aborted);
                            Report.OverallResult = CalibrationResult.Aborted;
                            Report.EndTime = DateTime.Now;
                            RecipeCompleted?.Invoke(this, Report);
                            return false;
                        case FailAction.Continue:
                            // Continue to next step
                            break;
                    }
                }
            }
            
            if (State != RecipeRunnerState.Aborted)
            {
                // Determine overall result
                var anyFailed = recipe.Steps.Any(s => s.Status == StepStatus.Failed);
                Report.OverallResult = anyFailed ? CalibrationResult.Failed : CalibrationResult.Passed;
                SetState(RecipeRunnerState.Completed);
            }
        }
        catch (OperationCanceledException)
        {
            _log.Info("Recipe execution cancelled");
            SetState(RecipeRunnerState.Aborted);
            Report.OverallResult = CalibrationResult.Aborted;
        }
        catch (Exception ex)
        {
            _log.Error($"Recipe execution error: {ex.Message}");
            SetState(RecipeRunnerState.Error);
            Report.OverallResult = CalibrationResult.Failed;
        }
        finally
        {
            Report.EndTime = DateTime.Now;
            RecipeCompleted?.Invoke(this, Report);
        }
        
        return Report.OverallResult == CalibrationResult.Passed;
    }
    
    private async Task<bool> ExecuteStepAsync(RecipeStep step)
    {
        step.Status = StepStatus.Running;
        StepStarted?.Invoke(this, step);
        
        _log.Info($"Executing step {step.Order}: {step.Name} ({step.Type})");
        
        try
        {
            StepResult result = step.Type switch
            {
                StepType.Configure => await ExecuteConfigureStepAsync(step),
                StepType.Measurement => await ExecuteMeasurementStepAsync(step),
                StepType.Wait => await ExecuteWaitStepAsync(step),
                StepType.Switch => await ExecuteSwitchStepAsync(step),
                StepType.Message => await ExecuteMessageStepAsync(step),
                _ => new StepResult { Passed = false, ErrorMessage = "Unknown step type" }
            };
            
            step.Result = result;
            step.Status = result.Passed ? StepStatus.Passed : StepStatus.Failed;
            
            // Add to report
            var (lower, upper) = step.Tolerance?.GetLimits() ?? (0, 0);
            Report?.StepReports.Add(new StepReport
            {
                StepOrder = step.Order,
                StepName = step.Name,
                StepType = step.Type,
                Status = step.Status,
                NominalValue = step.Tolerance?.Nominal,
                MeasuredValue = result.MeasuredValue,
                LowerLimit = step.Tolerance != null ? lower : null,
                UpperLimit = step.Tolerance != null ? upper : null,
                Deviation = result.Deviation,
                Unit = result.Unit,
                Timestamp = result.Timestamp,
                ErrorMessage = result.ErrorMessage
            });
            
            StepCompleted?.Invoke(this, step);
            
            if (result.Passed)
            {
                _log.Info($"Step {step.Order} PASSED");
            }
            else
            {
                _log.Warning($"Step {step.Order} FAILED: {result.ErrorMessage}");
            }
            
            return result.Passed;
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.Result = new StepResult { Passed = false, ErrorMessage = ex.Message };
            _log.Error($"Step {step.Order} exception: {ex.Message}");
            StepCompleted?.Invoke(this, step);
            return false;
        }
    }
    
    private async Task<StepResult> ExecuteConfigureStepAsync(RecipeStep step)
    {
        var device = _testBench.GetDeviceByRole(step.DeviceRole ?? "");
        if (device == null)
        {
            return new StepResult { Passed = false, ErrorMessage = $"Device role not found: {step.DeviceRole}" };
        }
        
        if (device is ISourceDevice source)
        {
            var parameters = new SourceParameters
            {
                Value = step.Parameters?.Voltage ?? step.Parameters?.Current ?? step.Parameters?.Resistance ?? 0,
                Unit = step.Parameters?.Unit ?? "V",
                Frequency = step.Parameters?.Frequency
            };
            
            // Determine source type from parameters
            if (step.Parameters?.Voltage.HasValue == true)
            {
                parameters.Type = step.Parameters.Frequency.HasValue ? SourceType.VoltageAC : SourceType.VoltageDC;
            }
            else if (step.Parameters?.Current.HasValue == true)
            {
                parameters.Type = step.Parameters.Frequency.HasValue ? SourceType.CurrentAC : SourceType.CurrentDC;
            }
            else if (step.Parameters?.Resistance.HasValue == true)
            {
                parameters.Type = SourceType.Resistance;
            }
            
            await source.SetOutputAsync(parameters);
            await source.EnableOutputAsync();
            
            return new StepResult { Passed = true };
        }
        
        return new StepResult { Passed = false, ErrorMessage = "Device is not a source" };
    }
    
    private async Task<StepResult> ExecuteMeasurementStepAsync(RecipeStep step)
    {
        var device = _testBench.GetDeviceByRole(step.DeviceRole ?? "");
        if (device == null)
        {
            return new StepResult { Passed = false, ErrorMessage = $"Device role not found: {step.DeviceRole}" };
        }
        
        if (device is IMeasurementDevice meter)
        {
            var measurementType = ParseMeasurementType(step.Parameters?.MeasurementType);
            
            var parameters = new MeasurementParameters
            {
                Type = measurementType,
                Range = step.Parameters?.Range ?? 0,
                Unit = step.Parameters?.Unit ?? "V"
            };
            
            var measurement = await meter.MeasureAsync(parameters);
            
            if (!measurement.IsValid)
            {
                return new StepResult 
                { 
                    Passed = false, 
                    ErrorMessage = measurement.ErrorMessage ?? "Invalid measurement" 
                };
            }
            
            // Check tolerance if defined
            if (step.Tolerance != null)
            {
                var passed = step.Tolerance.IsWithinTolerance(measurement.Value);
                var deviation = measurement.Value - step.Tolerance.Nominal;
                
                return new StepResult
                {
                    Passed = passed,
                    MeasuredValue = measurement.Value,
                    Unit = measurement.Unit,
                    Deviation = deviation,
                    ErrorMessage = passed ? null : $"Out of tolerance: {measurement.Value:F6} {measurement.Unit}"
                };
            }
            
            return new StepResult
            {
                Passed = true,
                MeasuredValue = measurement.Value,
                Unit = measurement.Unit
            };
        }
        
        return new StepResult { Passed = false, ErrorMessage = "Device is not a measurement device" };
    }
    
    private async Task<StepResult> ExecuteWaitStepAsync(RecipeStep step)
    {
        var duration = step.DurationMs ?? 1000;
        _log.Debug($"Waiting {duration}ms");
        await Task.Delay(duration, _cts?.Token ?? CancellationToken.None);
        return new StepResult { Passed = true };
    }
    
    private async Task<StepResult> ExecuteSwitchStepAsync(RecipeStep step)
    {
        var device = _testBench.GetDeviceByRole(step.DeviceRole ?? "SwitchMatrix");
        if (device == null)
        {
            return new StepResult { Passed = false, ErrorMessage = $"Switch device not found" };
        }
        
        if (device is ISwitchDevice switchDevice)
        {
            if (step.Parameters?.RouteName != null)
            {
                await switchDevice.SetChannelAsync(step.Parameters.RouteName);
            }
            else if (step.Parameters?.Channel.HasValue == true)
            {
                await switchDevice.SetChannelAsync(step.Parameters.Channel.Value);
            }
            
            return new StepResult { Passed = true };
        }
        
        return new StepResult { Passed = false, ErrorMessage = "Device is not a switch" };
    }
    
    private Task<StepResult> ExecuteMessageStepAsync(RecipeStep step)
    {
        _log.Info($"Message: {step.Name}");
        return Task.FromResult(new StepResult { Passed = true });
    }
    
    private async Task<FailAction> HandleFailedStepAsync(RecipeStep step)
    {
        var args = new FailedStepEventArgs(step);
        StepFailed?.Invoke(this, args);
        
        // Wait for user response if prompting
        if (step.OnFail == FailAction.Prompt)
        {
            // In a real app, this would wait for user input
            // For now, return the default from the event args
            await Task.Delay(100);
            return args.SelectedAction;
        }
        
        return step.OnFail switch
        {
            FailAction.Retry => FailAction.Retry,
            FailAction.Abort => FailAction.Abort,
            FailAction.Shutdown => FailAction.Shutdown,
            FailAction.Continue => FailAction.Continue,
            _ => FailAction.Abort
        };
    }
    
    private MeasurementType ParseMeasurementType(string? typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return MeasurementType.VoltageDC;
        
        return typeString.ToLowerInvariant() switch
        {
            "voltagedc" or "vdc" => MeasurementType.VoltageDC,
            "voltageac" or "vac" => MeasurementType.VoltageAC,
            "currentdc" or "idc" => MeasurementType.CurrentDC,
            "currentac" or "iac" => MeasurementType.CurrentAC,
            "resistance" or "ohm" => MeasurementType.Resistance,
            "frequency" or "freq" => MeasurementType.Frequency,
            _ => MeasurementType.VoltageDC
        };
    }
    
    public void Pause()
    {
        if (State == RecipeRunnerState.Running)
        {
            _isPaused = true;
            SetState(RecipeRunnerState.Paused);
            _log.Info("Recipe paused");
        }
    }
    
    public void Resume()
    {
        if (State == RecipeRunnerState.Paused)
        {
            _isPaused = false;
            SetState(RecipeRunnerState.Running);
            _log.Info("Recipe resumed");
        }
    }
    
    public void Abort()
    {
        _log.Warning("Aborting recipe...");
        _cts?.Cancel();
    }
    
    private void SetState(RecipeRunnerState newState)
    {
        State = newState;
        StateChanged?.Invoke(this, newState);
    }
}

public enum RecipeRunnerState
{
    Idle,
    Running,
    Paused,
    Completed,
    Aborted,
    Error
}

public class FailedStepEventArgs : EventArgs
{
    public RecipeStep Step { get; }
    public FailAction SelectedAction { get; set; } = FailAction.Abort;
    
    public FailedStepEventArgs(RecipeStep step)
    {
        Step = step;
    }
}
