using CalibrationDevices.Logging;
using CalibriCore.Models;
using CalibrationDevices.Interfaces;

namespace CalibriCore.Services;

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
        _log = logService ?? throw new ArgumentNullException(nameof(logService));
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
                Value = step.Parameters?.Voltage ?? step.Parameters?.Current ?? 0,
                Unit = step.Parameters?.Unit ?? "V"
            };

            // Determine source type from parameters
            if (step.Parameters?.Voltage.HasValue == true)
            {
                parameters.Type = SourceType.VoltageDC;
            }
            else if (step.Parameters?.Current.HasValue == true)
            {
                parameters.Type = SourceType.CurrentDC;
            }

            await source.SetOutputAsync(parameters);
            await source.EnableOutputAsync();

            return new StepResult { Passed = true };
        }

        return new StepResult { Passed = false, ErrorMessage = "Device is not a source" };
    }

    private async Task<StepResult> ExecuteMeasurementStepAsync(RecipeStep step)
    {
        var p = step.Parameters;

        // Calibration mode: measure both Reference and DUT
        if (!string.IsNullOrEmpty(p?.ReferenceDeviceRole) && !string.IsNullOrEmpty(p?.DUTDeviceRole))
        {
            return await ExecuteCalibrationMeasurementAsync(step);
        }

        // Single device measurement (legacy mode)
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

    /// <summary>
    /// Execute a calibration measurement: measure Reference (e.g., Keithley) then DUT (e.g., H&H).
    /// Compares the two values and checks against tolerance.
    /// </summary>
    private async Task<StepResult> ExecuteCalibrationMeasurementAsync(RecipeStep step)
    {
        var p = step.Parameters!;
        var refRole = p.ReferenceDeviceRole!;
        var dutRole = p.DUTDeviceRole!;

        _log.Debug($"Calibration measurement: Reference={refRole}, DUT={dutRole}");

        // 1. Get reference device (e.g., Keithley as "Richtiger Wert")
        var refDevice = _testBench.GetDeviceByRole(refRole);
        if (refDevice is not IMeasurementDevice refMeter)
        {
            return new StepResult { Passed = false, ErrorMessage = $"Reference device not found or not a meter: {refRole}" };
        }

        // 2. Get DUT device (e.g., H&H as "Anzeige")
        var dutDevice = _testBench.GetDeviceByRole(dutRole);
        if (dutDevice is not IMeasurementDevice dutMeter)
        {
            return new StepResult { Passed = false, ErrorMessage = $"DUT device not found or not a meter: {dutRole}" };
        }

        // 3. Configure shunt if specified (for current measurements)
        if (p.ShuntResistance.HasValue)
        {
            _log.Debug($"Using shunt: {p.ShuntResistance} Ω");
        }

        // 4. Take multiple samples and average
        var sampleCount = Math.Max(1, p.SampleCount);
        var settlingTimeMs = p.SettlingTimeMs;

        // Measure Reference
        var refMeasurements = new List<double>();
        var refMeasurementType = ParseMeasurementType(p.MeasurementType);

        for (int i = 0; i < sampleCount; i++)
        {
            if (settlingTimeMs > 0 && i > 0)
                await Task.Delay(settlingTimeMs);

            var m = await refMeter.MeasureAsync(new MeasurementParameters
            {
                Type = refMeasurementType,
                Range = p.Range ?? 0,
                Unit = p.Unit
            });

            if (!m.IsValid)
            {
                return new StepResult { Passed = false, ErrorMessage = $"Reference measurement failed: {m.ErrorMessage}" };
            }
            refMeasurements.Add(m.Value);
        }

        var refValue = refMeasurements.Average();
        var refUnit = p.Unit;

        // Small settling time between reference and DUT measurement
        await Task.Delay(50);

        // Measure DUT
        var dutMeasurements = new List<double>();
        for (int i = 0; i < sampleCount; i++)
        {
            if (settlingTimeMs > 0 && i > 0)
                await Task.Delay(settlingTimeMs);

            var m = await dutMeter.MeasureAsync(new MeasurementParameters
            {
                Type = refMeasurementType,
                Range = p.Range ?? 0,
                Unit = p.Unit
            });

            if (!m.IsValid)
            {
                return new StepResult { Passed = false, ErrorMessage = $"DUT measurement failed: {m.ErrorMessage}" };
            }
            dutMeasurements.Add(m.Value);
        }

        var dutValue = dutMeasurements.Average();
        var dutUnit = p.Unit;

        // 5. Calculate deviation
        double? deviation = null;
        if (refValue != 0)
        {
            deviation = ((dutValue - refValue) / refValue) * 100.0; // Percentage
        }

        // 6. Check tolerance
        bool passed = true;
        string? errorMessage = null;
        bool isWarning = false;

        if (step.Tolerance != null)
        {
            passed = step.Tolerance.IsWithinTolerance(dutValue);
            
            if (!passed)
            {
                errorMessage = $"DUT {dutValue:F6} {dutUnit} out of tolerance (ref: {refValue:F6} {refUnit})";
            }
            else if (deviation.HasValue)
            {
                // Warn if within tolerance but >70% of limit
                var (lower, upper) = step.Tolerance.GetLimits();
                var toleranceRange = upper - lower;
                var usedRange = Math.Max(dutValue - lower, upper - dutValue);
                if (toleranceRange > 0 && usedRange > toleranceRange * 0.7)
                {
                    isWarning = true;
                    _log.Warning($"Step {step.Order} at {usedRange/toleranceRange*100:F1}% of tolerance");
                }
            }
        }

        // 7. Calculate uncertainty (simplified)
        double? uncertainty = null;
        if (refMeasurements.Count > 1)
        {
            var stdDev = CalculateStdDev(refMeasurements);
            uncertainty = stdDev / Math.Sqrt(refMeasurements.Count) * 2; // Expanded uncertainty (k=2)
        }

        _log.Info($"Calibration: Reference={refValue:F6} {refUnit}, DUT={dutValue:F6} {dutUnit}, Deviation={deviation:F4}%, Passed={passed}");

        return new StepResult
        {
            Passed = passed,
            MeasuredValue = dutValue,
            Unit = dutUnit,
            ReferenceValue = refValue,
            DUTValue = dutValue,
            Deviation = deviation,
            Uncertainty = uncertainty,
            IsWarning = isWarning,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now
        };
    }

    private static double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        var avg = values.Average();
        var sumSquares = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSquares / (values.Count - 1));
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
            "currentdc" or "idc" => MeasurementType.CurrentDC,
            "currentshunt" or "shunt" => MeasurementType.CurrentShunt,
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
