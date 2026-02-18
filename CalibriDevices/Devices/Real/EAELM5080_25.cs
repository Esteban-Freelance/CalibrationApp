using CalibrationDevices.Interfaces;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace CalibriDevices.Devices.Real;
public class EAELM5080_25: IMeasurementDevice
{
    private readonly TcpConfig _config;
    private TcpClient? _client;
    private NetworkStream? _stream;

    // Prevent interleaved SCPI traffic
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    // Cache which MEAS commands work (firmware dependent)
    private string? _measVoltCmd; // "MEAS:VOLT?" or "MEAS:U?"
    private string? _measCurrCmd; // "MEAS:CURR?" or "MEAS:I?"

    private volatile DeviceStatus _status = DeviceStatus.Disconnected;

    public EAELM5080_25(string id, string name, TcpConfig config)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    // -------------------------
    // IDevice
    // -------------------------
    public string Id { get; }
    public string Name { get; }
    public string DeviceType => "ElectronicLoad.EA.ELM5080-25";
    public DeviceStatus Status => _status;

    public async Task<bool> ConnectAsync()
    {
        if (_status == DeviceStatus.Connected && IsSocketConnected())
            return true;

        _status = DeviceStatus.Connecting;

        try
        {
            await DisconnectAsync().ConfigureAwait(false);

            _client = new TcpClient { NoDelay = true };

            // Connect with timeout
            var connectTask = _client.ConnectAsync(_config.IpAddress, _config.Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(_config.TimeoutMs))
                                      .ConfigureAwait(false);

            if (completed != connectTask)
                throw new TimeoutException($"Timeout connecting to {_config.IpAddress}:{_config.Port}");

            _stream = _client.GetStream();
            _stream.ReadTimeout = _config.TimeoutMs;
            _stream.WriteTimeout = _config.TimeoutMs;

            // Best-effort: lock remote control so write commands are accepted
            try { await WriteAsync("SYST:LOCK ON").ConfigureAwait(false); }
            catch { /* some firmwares might not require it */ }

            // Detect MEAS query mnemonics once
            await DetectMeasurementMnemonicsAsync().ConfigureAwait(false);

            _status = DeviceStatus.Connected;
            return true;
        }
        catch
        {
            _status = DeviceStatus.Error;
            await DisconnectAsync().ConfigureAwait(false);
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        try { _stream?.Dispose(); } catch { }
        try { _client?.Close(); } catch { }

        _stream = null;
        _client = null;

        _measVoltCmd = null;
        _measCurrCmd = null;

        _status = DeviceStatus.Disconnected;
        return Task.CompletedTask;
    }

    public async Task ResetAsync()
    {
        EnsureConnectedOrThrow();

        _status = DeviceStatus.Busy;
        try
        {
            // SCPI reset (device-dependent, but *RST is standard SCPI)
            await WriteAsync("*RST").ConfigureAwait(false);

            // After reset, remote lock may be lost; re-lock best effort
            try { await WriteAsync("SYST:LOCK ON").ConfigureAwait(false); } catch { }

            // Re-detect measurement commands
            await DetectMeasurementMnemonicsAsync().ConfigureAwait(false);

            _status = DeviceStatus.Connected;
        }
        catch
        {
            _status = DeviceStatus.Error;
            throw;
        }
    }

    public async Task<string> GetIdentificationAsync()
    {
        EnsureConnectedOrThrow();

        _status = DeviceStatus.Busy;
        try
        {
            var idn = await QueryAsync("*IDN?").ConfigureAwait(false);
            _status = DeviceStatus.Connected;
            return idn.Trim();
        }
        catch
        {
            _status = DeviceStatus.Error;
            throw;
        }
    }

    // -------------------------
    // IMeasurementDevice
    // -------------------------
    public async Task<MeasurementResult> MeasureAsync(MeasurementParameters parameters)
    {
        if (parameters == null) throw new ArgumentNullException(nameof(parameters));

        if (!IsSocketConnected())
        {
            var ok = await ConnectAsync().ConfigureAwait(false);
            if (!ok)
            {
                return new MeasurementResult
                {
                    IsValid = false,
                    ErrorMessage = "Could not connect to EAELM5080_25.",
                    Timestamp = DateTime.Now
                };
            }
        }

        if (parameters.Type is not MeasurementType.VoltageDC and not MeasurementType.CurrentDC)
        {
            return new MeasurementResult
            {
                IsValid = false,
                ErrorMessage = $"MeasurementType '{parameters.Type}' not supported by EAELM5080_25 internal measurement.",
                Timestamp = DateTime.Now
            };
        }

        var unit = parameters.Type == MeasurementType.VoltageDC ? "V" : "A";

        _status = DeviceStatus.Busy;

        try
        {
            if (parameters.SettlingTime > TimeSpan.Zero)
                await Task.Delay(parameters.SettlingTime).ConfigureAwait(false);

            int n = Math.Max(1, parameters.SampleCount);
            var values = new double[n];

            for (int i = 0; i < n; i++)
            {
                values[i] = parameters.Type == MeasurementType.VoltageDC
                    ? await QueryMeasuredVoltageAsync().ConfigureAwait(false)
                    : await QueryMeasuredCurrentAsync().ConfigureAwait(false);
            }

            var avg = values.Average();

            _status = DeviceStatus.Connected;

            return new MeasurementResult
            {
                Value = avg,
                Unit = string.IsNullOrWhiteSpace(parameters.Unit) ? unit : parameters.Unit,
                Timestamp = DateTime.Now,
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            string? devErr = null;
            try { devErr = await QueryAsync("SYST:ERR?").ConfigureAwait(false); } catch { }

            _status = DeviceStatus.Error;

            return new MeasurementResult
            {
                IsValid = false,
                ErrorMessage = devErr == null ? ex.Message : $"{ex.Message} | Device: {devErr.Trim()}",
                Timestamp = DateTime.Now,
                Unit = string.IsNullOrWhiteSpace(parameters.Unit) ? unit : parameters.Unit
            };
        }
    }

    public Task<IEnumerable<MeasurementRange>> GetAvailableRangesAsync(MeasurementType type)
    {
        // EA load doesn't provide DMM-like ranges. Provide a simple "Auto" for DC V/I.
        if (type is MeasurementType.VoltageDC or MeasurementType.CurrentDC)
        {
            IEnumerable<MeasurementRange> ranges = new[]
            {
                    new MeasurementRange
                    {
                        Name = "Auto",
                        MinValue = 0,
                        MaxValue = double.PositiveInfinity,
                        Resolution = 0
                    }
                };
            return Task.FromResult(ranges);
        }

        return Task.FromResult(Enumerable.Empty<MeasurementRange>());
    }

    public Task SetRangeAsync(MeasurementType type, MeasurementRange range)
        => throw new NotSupportedException("EAELM5080_25 does not support manual measurement range switching.");

    // -------------------------
    // Measurement query helpers
    // -------------------------
    private async Task DetectMeasurementMnemonicsAsync()
    {
        // Try newer names first, then fallbacks
        _measVoltCmd = await FirstWorkingNumericQueryAsync(new[] { "MEAS:VOLT?", "MEAS:U?" }).ConfigureAwait(false);
        _measCurrCmd = await FirstWorkingNumericQueryAsync(new[] { "MEAS:CURR?", "MEAS:I?" }).ConfigureAwait(false);
    }

    private async Task<string?> FirstWorkingNumericQueryAsync(IEnumerable<string> candidates)
    {
        foreach (var cmd in candidates)
        {
            try
            {
                _ = await QueryDoubleAsync(cmd).ConfigureAwait(false);
                return cmd;
            }
            catch
            {
                // Best-effort: clear error queue so it doesn't stack up
                try { _ = await QueryAsync("SYST:ERR?").ConfigureAwait(false); } catch { }
            }
        }
        return null;
    }

    private Task<double> QueryMeasuredVoltageAsync()
    {
        var cmd = _measVoltCmd ?? "MEAS:VOLT?";
        return QueryDoubleAsync(cmd);
    }

    private Task<double> QueryMeasuredCurrentAsync()
    {
        var cmd = _measCurrCmd ?? "MEAS:CURR?";
        return QueryDoubleAsync(cmd);
    }

    // -------------------------
    // Raw SCPI over TCP
    // -------------------------
    private async Task WriteAsync(string command)
    {
        EnsureConnectedOrThrow();
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var bytes = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
            await _stream!.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<string> QueryAsync(string command)
    {
        EnsureConnectedOrThrow();
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var outBytes = Encoding.ASCII.GetBytes(command.TrimEnd() + "\n");
            await _stream!.WriteAsync(outBytes, 0, outBytes.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);

            return await ReadLineAsync(_stream!, _config.TimeoutMs).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<double> QueryDoubleAsync(string command)
    {
        var s = await QueryAsync(command).ConfigureAwait(false);
        if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;

        throw new FormatException($"Could not parse numeric response '{s}' for '{command}'.");
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream, int timeoutMs)
    {
        var buffer = new byte[256];
        var sb = new StringBuilder();
        using var cts = new CancellationTokenSource(timeoutMs);

        while (true)
        {
            int n = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
            if (n <= 0) throw new SocketException((int)SocketError.ConnectionReset);

            sb.Append(Encoding.ASCII.GetString(buffer, 0, n));
            var text = sb.ToString();
            int idx = text.IndexOf('\n');
            if (idx >= 0)
            {
                var line = text.Substring(0, idx);
                return line.TrimEnd('\r');
            }
        }
    }

    private bool IsSocketConnected()
        => _client?.Connected == true && _stream != null;

    private void EnsureConnectedOrThrow()
    {
        if (!IsSocketConnected())
            throw new InvalidOperationException("Device is not connected.");
    }

    public void Dispose()
    {
        _ioLock.Dispose();
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
    }
}