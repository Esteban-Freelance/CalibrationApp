using CalibrationDevices.Devices.Mocking;
using CalibrationDevices.Logging;
using CalibriCore.Models;
using CalibrationDevices.Logging;
using CalibrationDevices.Interfaces;

namespace CalibriCore;

/// <summary>
/// Factory for creating device instances based on configuration.
/// </summary>
public static class DeviceFactory
{
    public static IDevice CreateDevice(DeviceConfig config, ILogService? logService = null)
    {
        var log = logService ?? throw new ArgumentNullException(nameof(logService));

        return config.Driver.ToLowerInvariant() switch
        {
            "mockmultimeter" or "mockmeasurementdevice" =>
                new MockMeasurementDevice(config.Id, config.Name, log),

            "mockcalibrator" or "mocksourcedevice" =>
                new MockSourceDevice(config.Id, config.Name, log),

            "mockswitchmatrix" or "mockswitchdevice" =>
                new MockSwitchDevice(config.Id, config.Name, log),

            "mockhvsource" or "mockhighvoltagesource" =>
                new MockHighVoltageSource(config.Id, config.Name, log),

            _ => throw new ArgumentException($"Unknown driver type: {config.Driver}")
        };
    }
}
