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

        var driver = config.Driver.ToLowerInvariant();

        if (driver == "mockmultimeter" || driver == "mockmeasurementdevice")
            return new MockMeasurementDevice(config.Id, config.Name, log);

        if (driver == "mockcalibrator" || driver == "mocksourcedevice")
            return new MockSourceDevice(config.Id, config.Name, log);

        if (driver == "mockswitchmatrix" || driver == "mockswitchdevice")
            return new MockSwitchDevice(config.Id, config.Name, log);

        if (driver == "mockhvsource" || driver == "mockhighvoltagesource")
            return new MockHighVoltageSource(config.Id, config.Name, log);

        if (driver == "eaps8720u")
            return new EAPS8720U(config.Id, config.Name, CreateTcpConfig(config.Connection), log);

        if (driver == "keithley3706a")
            return new Keithley3706A(config.Id, config.Name, CreateTcpConfig(config.Connection), log);

        if (driver == "microchiprelaycard")
            return new MicrochipRelayCard(config.Id, config.Name, CreateRelayCardConfig(config.Connection), log);

        if (driver == "hhload" || driver == "hhmeasurementdevice")
            return new HhMeasurementDevice(config.Id, CreateVisaResource(config.Connection), log);

        throw new ArgumentException($"Unknown driver type: {config.Driver}");
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

        var type = conn.Type?.ToUpperInvariant() ?? "";
        
        // GPIB: GPIB0::1::INSTR
        if (type == "GPIB")
        {
            var board = conn.Board ?? 0;
            return $"GPIB{board}::{conn.Address}::INSTR";
        }

        // TCP/IP: TCPIP0::192.168.1.50::5025::INSTR
        var port = int.TryParse(conn.Port, out var p) ? p : 5025;
        return $"TCPIP0::{conn.Address}::{port}::INSTR";
    }
}
