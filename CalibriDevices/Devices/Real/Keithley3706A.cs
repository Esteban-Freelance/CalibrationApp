using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace CalibrationDevices.Devices.Real
{
    public class Keithley3706A : IMeasurementDevice
    {
        private readonly TcpConfig _config;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private DeviceStatus _status = DeviceStatus.Disconnected;
        private ILogService _log;

        public string Id { get; }
        public string Name { get; }
        public string DeviceType => "DMM";
        public DeviceStatus Status => _status;

        public Keithley3706A(string id, string name, TcpConfig config, ILogService logService = null)
        {
            Id = id;
            Name = name;
            _config = config;
            _log = logService ?? NullLogService.Instance;
        }

        // ──────────────────────────────────────────────
        //  IDevice
        // ──────────────────────────────────────────────

        public async Task<bool> ConnectAsync()
        {
            _status = DeviceStatus.Connecting;
            try
            {
                _client = new TcpClient();
                _client.ReceiveTimeout = _config.TimeoutMs;
                _client.SendTimeout = _config.TimeoutMs;

                await _client.ConnectAsync(_config.IpAddress, _config.Port);
                _stream = _client.GetStream();

                // Small delay for connection to stabilize
                await Task.Delay(200);

                // Drain any welcome message
                if (_stream.DataAvailable)
                {
                    var drain = new byte[4096];
                    _stream.Read(drain, 0, drain.Length);
                }

                // Verify connection with *IDN?
                string idn = await Query("*IDN?");
                if (string.IsNullOrEmpty(idn))
                {
                    _status = DeviceStatus.Error;
                    return false;
                }

                _status = DeviceStatus.Connected;
                return true;
            }
            catch
            {
                _status = DeviceStatus.Error;
                return false;
            }
        }

        public Task DisconnectAsync()
        {
            _stream?.Dispose();
            _client?.Dispose();
            _client = null;
            _stream = null;
            _status = DeviceStatus.Disconnected;
            return Task.CompletedTask;
        }

        public async Task ResetAsync()
        {
            // Reset DMM to factory defaults
            await Send("dmm.reset(\"all\")");
            await Send("*CLS");
            await Task.Delay(500);
        }

        public async Task<string> GetIdentificationAsync()
        {
            return await Query("*IDN?");
        }

        // ──────────────────────────────────────────────
        //  IMeasurementDevice
        // ──────────────────────────────────────────────

        public async Task<MeasurementResult> MeasureAsync(MeasurementParameters parameters)
        {
            if (_status != DeviceStatus.Connected)
                throw new InvalidOperationException("Device not connected");

            ValidateMeasurementType(parameters.Type);

            try
            {
                _status = DeviceStatus.Busy;

                double value;

                if (parameters.Type == MeasurementType.CurrentShunt)
                {
                    // Calculated current: measure voltage across shunt, then I = V / R
                    value = await MeasureShuntCurrentAsync(parameters);
                }
                else
                {
                    // Direct DMM measurement (DCV, DCI, ACV, Resistance, etc.)
                    value = await MeasureDirectAsync(parameters);
                }

                // Optional settling delay after measurement
                if (parameters.SettlingTime > TimeSpan.Zero)
                    await Task.Delay(parameters.SettlingTime);

                _status = DeviceStatus.Connected;

                return new MeasurementResult
                {
                    Value = value,
                    Unit = GetUnit(parameters.Type),
                    Timestamp = DateTime.Now,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                _log.Error($"Measurement failed: {ex.Message}", Name);
                _status = DeviceStatus.Connected;

                return new MeasurementResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Performs a direct DMM measurement (voltage, current, resistance, etc.)
        /// </summary>
        private async Task<double> MeasureDirectAsync(MeasurementParameters parameters)
        {
            string funcConst = GetTspFunction(parameters.Type);
            await Send($"dmm.func = {funcConst}");
            await Send($"dmm.range = {FormatNumber(parameters.Range)}");
            await Send("dmm.autodelay = dmm.ON");
            await Send($"dmm.close(\"{parameters.Channel}\")");

            return await TakeReadingAsync(parameters.SampleCount);
        }

        /// <summary>
        /// Calculates current by measuring voltage across a known shunt resistor.
        /// I = V_shunt / R_shunt
        /// </summary>
        private async Task<double> MeasureShuntCurrentAsync(MeasurementParameters parameters)
        {
            if (parameters.ShuntResistance is null or <= 0)
                throw new ArgumentException("ShuntResistance must be set and > 0 for CurrentShunt measurement");

            // Measure voltage across the shunt
            await Send("dmm.func = dmm.DC_VOLTS");
            await Send($"dmm.range = {FormatNumber(parameters.Range)}");
            await Send("dmm.autodelay = dmm.ON");
            await Send($"dmm.close(\"{parameters.Channel}\")");

            double voltage = await TakeReadingAsync(parameters.SampleCount);

            return voltage / parameters.ShuntResistance.Value;
        }

        /// <summary>
        /// Takes one or more readings and returns the (averaged) value.
        /// </summary>
        private async Task<double> TakeReadingAsync(int sampleCount)
        {
            if (sampleCount <= 1)
            {
                string response = await Query("print(dmm.measure())");
                return ParseDouble(response);
            }

            await Send($"buf = dmm.makebuffer({sampleCount})");
            await Send("buf.clear()");
            await Send("buf.appendmode = 1");
            await Send($"dmm.measurecount = {sampleCount}");
            await Send("dmm.measure(buf)");

            string response2 = await Query(
                $"sum = 0 for x = 1, buf.n do sum = sum + buf[x] end print(sum / buf.n)");
            double value = ParseDouble(response2);

            // Clean up
            await Send("dmm.measurecount = 1");
            await Send("buf = nil");

            return value;
        }

        private string FormatNumber(double value)
        {
            return value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }

        public Task<IEnumerable<MeasurementRange>> GetAvailableRangesAsync(MeasurementType type)
        {
            ValidateMeasurementType(type);

            List<MeasurementRange> ranges = type switch
            {
                // 3706A DC Voltage ranges
                MeasurementType.VoltageDC =>
                [
                    new() { Name = "Auto",   MinValue = 0,      MaxValue = 300,     Resolution = 0 },
                new() { Name = "100 mV",  MinValue = 0,      MaxValue = 0.1,     Resolution = 0.000001 },
                new() { Name = "1 V",     MinValue = 0,      MaxValue = 1,       Resolution = 0.00001 },
                new() { Name = "10 V",    MinValue = 0,      MaxValue = 10,      Resolution = 0.0001 },
                new() { Name = "100 V",   MinValue = 0,      MaxValue = 100,     Resolution = 0.001 },
                new() { Name = "300 V",   MinValue = 0,      MaxValue = 300,     Resolution = 0.01 }
                ],

                // 3706A DC Current ranges
                MeasurementType.CurrentDC =>
                [
                    new() { Name = "Auto",   MinValue = 0,       MaxValue = 3,       Resolution = 0 },
                new() { Name = "1 µA",   MinValue = 0,       MaxValue = 0.000001, Resolution = 0.000000001 },
                new() { Name = "10 µA",  MinValue = 0,       MaxValue = 0.00001,  Resolution = 0.00000001 },
                new() { Name = "100 µA", MinValue = 0,       MaxValue = 0.0001,   Resolution = 0.0000001 },
                new() { Name = "1 mA",   MinValue = 0,       MaxValue = 0.001,    Resolution = 0.000001 },
                new() { Name = "10 mA",  MinValue = 0,       MaxValue = 0.01,     Resolution = 0.00001 },
                new() { Name = "100 mA", MinValue = 0,       MaxValue = 0.1,      Resolution = 0.0001 },
                new() { Name = "1 A",    MinValue = 0,       MaxValue = 1,        Resolution = 0.001 },
                new() { Name = "3 A",    MinValue = 0,       MaxValue = 3,        Resolution = 0.01 }
                ],

                _ => throw new NotSupportedException($"{type} not supported")
            };

            return Task.FromResult<IEnumerable<MeasurementRange>>(ranges);
        }

        public async Task SetRangeAsync(MeasurementType type, MeasurementRange range)
        {
            if (_status != DeviceStatus.Connected)
                throw new InvalidOperationException("Device not connected");

            ValidateMeasurementType(type);

            // Set the function first
            string funcConst = GetTspFunction(type);
            await Send($"dmm.func = {funcConst}");

            if (range.Name.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                await Send("dmm.autorange = dmm.ON");
            }
            else
            {
                await Send("dmm.autorange = dmm.OFF");
                // Set range to the max value of the selected range
                await Send($"dmm.range = {range.MaxValue.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        // ──────────────────────────────────────────────
        //  Raw command access (not part of interface)
        // ──────────────────────────────────────────────

        /// <summary>Send a raw TSP/SCPI command and return the response (if any).</summary>
        public async Task<string> SendRaw(string command)
        {
            if (command.Contains("print(") || command.TrimEnd().EndsWith("?"))
                return await Query(command);

            await Send(command);
            return "";
        }

        // ──────────────────────────────────────────────
        //  TCP communication
        // ──────────────────────────────────────────────

        private async Task Send(string command)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected");

            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            await _stream.WriteAsync(data);
            await Task.Delay(50); // Small delay between commands
        }

        private async Task<string> Query(string command)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected");

            // Clear any pending data
            if (_stream.DataAvailable)
            {
                var drain = new byte[4096];
                await _stream.ReadAsync(drain);
            }

            // Send command
            byte[] data = Encoding.ASCII.GetBytes(command + "\n");
            await _stream.WriteAsync(data);

            // Read response
            var sb = new StringBuilder();
            var buffer = new byte[4096];
            var deadline = DateTime.UtcNow.AddMilliseconds(_config.TimeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (_stream.DataAvailable)
                {
                    int bytesRead = await _stream.ReadAsync(buffer);
                    sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                    // Check if we got a complete line (ends with \n)
                    if (sb.ToString().Contains('\n'))
                        break;

                    // Reset deadline on data received
                    deadline = DateTime.UtcNow.AddMilliseconds(500);
                }
                else
                {
                    await Task.Delay(10);
                }
            }

            return sb.ToString().Trim();
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        private static string GetTspFunction(MeasurementType type) => type switch
        {
            MeasurementType.VoltageDC => "dmm.DC_VOLTS",
            MeasurementType.CurrentDC => "dmm.DC_AMPS",
            _ => throw new NotSupportedException($"{type} not supported on this device")
        };

        private static string GetUnit(MeasurementType type) => type switch
        {
            MeasurementType.VoltageDC => "V",
            MeasurementType.CurrentShunt => "A",
            _ => ""
        };

        private static void ValidateMeasurementType(MeasurementType type)
        {
            if (type is not (MeasurementType.VoltageDC or MeasurementType.CurrentShunt))
                throw new NotSupportedException(
                    $"{type} is not supported. This device only supports VoltageDC and CurrentDC.");
        }

        private static double ParseDouble(string response)
        {
            string cleaned = response.Trim();

            if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return val;

            throw new FormatException($"Could not parse measurement value: '{cleaned}'");
        }
    }
}

public class TcpConfig
{
    public string IpAddress { get; set; } = "192.168.1.50";
    public int Port { get; set; } = 5025;          // Standard SCPI raw socket port
    public int TimeoutMs { get; set; } = 5000;
}
