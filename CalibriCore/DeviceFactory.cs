using CalibrationDevices.Devices.Mocking;
using CalibrationDevices.Logging;
using CalibriCore.Models;
using CalibrationDevices.Logging;
using CalibrationDevices.Interfaces;
using CalibrationDevices.Devices.Real;

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

            // Real devices - Power Supplies
            "eaps8720u" =>
                new EAPS8720U(config.Id, config.Name, log),

            // Real devices - Reference DMM
            "keithley3706a" =>
                new Keithley3706A(config.Id, config.Name, log),

            // Real devices - Relay Card
            "microchiprelaycard" =>
                new MicroChipRelayCard(config.Id, config.Name, log),

            // Real devices - Electronic Load (DUT) - H&H ZS 530-3
            "hhload" or "hhloadzs530" or "h&h" or "hhmeasurementdevice" =>
                new HhHMeasurementDevice(config.Id, config.ConnectionString),

            _ => throw new ArgumentException($"Unknown driver type: {config.Driver}")
        };
    }
}
