using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;
using System.Net.Sockets;
using System.Text;

namespace CalibrationDevices.Devices.Real;
public class EAPS8720U : ISourceDevice
{
    private readonly TcpConfig _config;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly ILogService _log;

    private DeviceStatus _status = DeviceStatus.Disconnected;
    private readonly object _commLock = new();

    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "PowerSupply";
    public DeviceStatus Status => _status;

    public EAPS8720U(
    string id,
    string name,
    TcpConfig config,
    ILogService? logService = null)
    {
        Id = id;
        Name = name;
        _config = config;
        _log = logService ?? NullLogService.Instance;
    }

    #region IDevice

    public async Task<bool> ConnectAsync()
    {
        if (_status == DeviceStatus.Connected)
            return true;

        _status = DeviceStatus.Connecting;
        _log.Info($"Connecting to EA-PS 8720-U at {_config.IpAddress}:{_config.Port}...", Name);

        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.ReceiveTimeout = _config.TimeoutMs;
            _tcpClient.SendTimeout = _config.TimeoutMs;

            await _tcpClient.ConnectAsync(_config.IpAddress, _config.Port);
            _stream = _tcpClient.GetStream();

            // Verify connection with identification query
            var idn = await QueryAsync("*IDN?");
            _log.Info($"Connected: {idn}", Name);

            await SendCommandAsync("*RST");

            _status = DeviceStatus.Connected;
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Connection failed: {ex.Message}", Name);
            _status = DeviceStatus.Error;
            Cleanup();
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        _log.Info("Disconnecting...", Name);

        try
        {
            // Try to leave remote control gracefully
            if (_status == DeviceStatus.Connected)
            {
                try { SendCommand("*RST"); }
                catch { /* best effort */ }
            }
        }
        finally
        {
            Cleanup();
            _status = DeviceStatus.Disconnected;
        }

        return Task.CompletedTask;
    }

    public async Task ResetAsync()
    {
        EnsureConnected();
        _log.Debug("Resetting device...", Name);
        await SendCommandAsync("*RST");
        // *RST sets U=0, I=0 and leaves remote control on EA devices,
        // so we may need to re-establish remote. Give it time to settle.
        await Task.Delay(500);
    }

    public async Task<string> GetIdentificationAsync()
    {
        EnsureConnected();
        return await QueryAsync("*IDN?");
    }

    #endregion

    #region ISourceDevice

    public async Task SetOutputAsync(SourceParameters parameters)
    {
        EnsureConnected();
        await SendCommandAsync("SYST:LOCK 1");        // not just LOCK ON
        await SendCommandAsync("OUTP OFF");
        await ConfigureSourceAsync(parameters);
        await SendCommandAsync("OUTP ON");
    }

    private async Task ConfigureSourceAsync(SourceParameters parameters)
    {
        switch (parameters.Type)
        {
            case SourceType.VoltageDC:
                _log.Debug($"Setting voltage to {parameters.Value} V", Name);
                await SendCommandAsync($"VOLT {parameters.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");

                if (parameters.CurrentLimit.HasValue)
                {
                    _log.Debug($"Setting current limit to {parameters.CurrentLimit.Value} A", Name);
                    var currentOne = $"SOURce:CURRent {parameters.CurrentLimit.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                    await SendCommandAsync(currentOne);
                }
                break;

            case SourceType.CurrentDC:
                _log.Debug($"Setting current to {parameters.Value} A", Name);
                var current = $"SOUR:CURR {parameters.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
                await SendCommandAsync(current);

                if (parameters.VoltageLimit.HasValue)
                {
                    _log.Debug($"Setting voltage limit to {parameters.VoltageLimit.Value} V", Name);
                    await SendCommandAsync($"VOLT {parameters.VoltageLimit.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
                }
                break;

            default:
                throw new NotSupportedException($"EA-PS 8720-U does not support source type: {parameters.Type}");
        }
    }

    public async Task EnableOutputAsync()
    {
        EnsureConnected();
        _log.Debug("Enabling output", Name);
        await SendCommandAsync("OUTP 1");
    }

    public async Task DisableOutputAsync()
    {
        EnsureConnected();
        _log.Debug("Disabling output", Name);
        await SendCommandAsync("OUTP 0");
    }

    public async Task<SourceStatus> GetOutputStatusAsync(SourceParameters parameters)
    {
        EnsureConnected();

        var voltage = await QueryDoubleAsync("MEAS:VOLT?");
        var current = await QueryDoubleAsync("MEAS:CURR?");

        return new SourceStatus
        {
            IsEnabled = true, // EA-PS doesn't have a direct OUTP? query on older models
            ActualValue = voltage,
            Unit = "V",
            InRegulation = true,
            OverloadProtection = false
        };
    }

    public Task<IEnumerable<SourceCapability>> GetCapabilitiesAsync()
    {
        // EA-PS 8720-U: 0-720V, 0-current depending on model
        var capabilities = new List<SourceCapability>
            {
                new SourceCapability
                {
                    Type = SourceType.VoltageDC,
                    MinValue = 0,
                    MaxValue = 720,
                    Resolution = 0.01,
                    Unit = "V"
                },
                new SourceCapability
                {
                    Type = SourceType.CurrentDC,
                    MinValue = 0,
                    MaxValue = 15, // Depends on specific model variant
                    Resolution = 0.001,
                    Unit = "A"
                }
            };

        return Task.FromResult<IEnumerable<SourceCapability>>(capabilities);
    }

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Reads the actual output voltage from the device.
    /// </summary>
    public async Task<double> MeasureVoltageAsync()
    {
        return await QueryDoubleAsync("MEAS:VOLT?");
    }

    /// <summary>
    /// Reads the actual output current from the device.
    /// </summary>
    public async Task<double> MeasureCurrentAsync()
    {
        return await QueryDoubleAsync("MEAS:CURR?");
    }

    /// <summary>
    /// Sets voltage and current in a single call, then enables output.
    /// </summary>
    public async Task SetVoltageAndCurrentAsync(double voltage, double current, bool enableOutput = true)
    {
        await SendCommandAsync($"VOLT {voltage.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
        await SendCommandAsync($"CURR {current.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");

        if (enableOutput)
            await EnableOutputAsync();
    }

    #endregion

    #region TCP/SCPI Communication

    private async Task SendCommandAsync(string command)
    {
        await Task.Run(() => SendCommand(command));
    }

    private void SendCommand(string command)
    {
        lock (_commLock)
        {
            if (_stream == null || !_tcpClient!.Connected)
                throw new InvalidOperationException("Not connected to device");

            var data = Encoding.ASCII.GetBytes(command + "\n");
            _stream.Write(data, 0, data.Length);
            _stream.Flush();

            _log.Debug($"TX: {command}", Name);
        }
    }

    private async Task<string> QueryAsync(string query)
    {
        return await Task.Run(() =>
        {
            lock (_commLock)
            {
                if (_stream == null || !_tcpClient!.Connected)
                    throw new InvalidOperationException("Not connected to device");

                // Send query
                var data = Encoding.ASCII.GetBytes(query + "\n");
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
                _log.Debug($"TX: {query}", Name);

                // Read response
                var buffer = new byte[4096];
                var response = new StringBuilder();
                var startTime = DateTime.UtcNow;

                while (true)
                {
                    if (_stream.DataAvailable)
                    {
                        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            response.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                            // Check if response is complete (contains LF or CR)
                            var text = response.ToString();
                            if (text.Contains('\n') || text.Contains('\r'))
                                break;
                        }
                    }
                    else
                    {
                        // Check timeout
                        if ((DateTime.UtcNow - startTime).TotalMilliseconds > _config.Port)
                            throw new TimeoutException($"No response for query: {query}");

                        Thread.Sleep(10);
                    }
                }

                var result = response.ToString().Trim();
                _log.Debug($"RX: {result}", Name);
                return result;
            }
        });
    }

    private async Task<double> QueryDoubleAsync(string query)
    {
        var response = await QueryAsync(query);
        var cleaned = response.Trim().Split(' ')[0];

        if (double.TryParse(cleaned, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double value))
        {
            return value;
        }

        _log.Warning($"Failed to parse response '{response}' as double", Name);
        return double.NaN;
    }

    #endregion

    #region Helpers

    private void EnsureConnected()
    {
        if (_status != DeviceStatus.Connected || _stream == null || _tcpClient == null || !_tcpClient.Connected)
            throw new InvalidOperationException($"EA-PS 8720-U '{Name}' is not connected. Status: {_status}");
    }

    private void Cleanup()
    {
        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }
        _stream = null;
        _tcpClient = null;
    }

    #endregion

}