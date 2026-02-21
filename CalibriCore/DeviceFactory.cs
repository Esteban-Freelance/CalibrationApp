using CalibrationDevices.Devices.Mocking;
using CalibrationDevices.Devices.Real;
using CalibrationDevices.Logging;
using CalibriCore.Models;
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

            // Real devices
            "eaps8720u" =>
                new EAPS8720U(
                    config.Id,
                    config.Name,
                    CreateTcpConfig(config.Connection),
                    log),

            "keithley3706a" =>
                new Keithley3706A(
                    config.Id,
                    config.Name,
                    CreateTcpConfig(config.Connection),
                    log),

            "microchiprelaycard" =>
                new MicrochipRelayCard(
                    config.Id,
                    config.Name,
                    CreateRelayCardConfig(config.Connection)),

            "hhload" or "hhmeasurementdevice" =>
                new HhMeasurementDevice(
                    config.Id,
                    CreateVisaResource(config.Connection)),

            _ => throw new ArgumentException($"Unknown driver type: {config.Driver}")
        };
    }

    private static TcpConfig CreateTcpConfig(ConnectionConfig? conn)
    {
        if (conn == null || string.IsNullOrEmpty(conn.Address))
        {
            throw new ArgumentException("TCP connection requires an Address in DeviceConfig.Connection");
        }

        return new TcpConfig
        {
            IpAddress = conn.Address,
            Port = int.TryParse(conn.Port, out var p) ? p : 5025,
            TimeoutMs = conn.TimeoutMs > 0 ? conn.TimeoutMs : 5000
        };
    }

    private static RelayCardConfig CreateRelayCardConfig(ConnectionConfig? conn)
    {
        if (conn == null || string.IsNullOrEmpty(conn.Address))
        {
            throw new ArgumentException("Relay card connection requires an Address in DeviceConfig.Connection");
        }

        return new RelayCardConfig
        {
            IpAddress = conn.Address,
            HttpPort = int.TryParse(conn.Port, out var p) ? p : 80,
            TimeoutMs = conn.TimeoutMs > 0 ? conn.TimeoutMs : 5000
        };
    }

    private static string CreateVisaResource(ConnectionConfig? conn)
    {
        if (conn == null || string.IsNullOrEmpty(conn.Address))
        {
            throw new ArgumentException("VISA connection requires an Address in DeviceConfig.Connection");
        }

        // Construct VISA resource string (e.g., "TCPIP0::192.168.1.50::INSTR")
        var port = int.TryParse(conn.Port, out var p) ? p : 5025;
        return $"TCPIP0::{conn.Address}::{port}::INSTR";
    }
}
