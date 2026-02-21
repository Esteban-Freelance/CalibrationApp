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
            // Mock devices
            "mockmultimeter" or "mockmeasurementdevice" =>
                new MockMeasurementDevice(config.Id, config.Name, log),

            "mockcalibrator" or "mocksourcedevice" =>
                new MockSourceDevice(config.Id, config.Name, log),

            "mockswitchmatrix" or "mockswitchdevice" =>
                new MockSwitchDevice(config.Id, config.Name, log),

            "mockhvsource" or "mockhighvoltagesource" =>
                new MockHighVoltageSource(config.Id, config.Name, log),

            // Real devices - requires proper infrastructure (VISA/TCP) setup
            // TODO: Fix constructors to match device interfaces
            // "eaps8720u" => new EAPS8720U(...),
            // "keithley3706a" => new Keithley3706A(...),
            // "microchiprelaycard" => new MicroChipRelayCard(...),
            // "hhload" or "hhmeasurementdevice" => new HhHMeasurementDevice(...),

            _ => throw new ArgumentException($"Unknown driver type: {config.Driver}")
        };
    }
}
