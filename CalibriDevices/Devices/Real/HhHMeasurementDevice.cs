namespace CalibrationDevices.Devices.Real;

    using CalibrationDevices.Interfaces;
    using NationalInstruments.Visa;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    public class HhMeasurementDevice : IMeasurementDevice, IDisposable
    {
        private readonly string _resource;
        private MessageBasedSession? _session;
        private readonly string _id;
        private string _name = "Unknown";
        private DeviceStatus _status = DeviceStatus.Disconnected;

        public string Id => _id;
        public string Name => _name;
        public string DeviceType => "Load";
        public DeviceStatus Status => _status;

        public HhMeasurementDevice(string id, string resource)
        {
            _id = id;
            _resource = resource;
        }

        public async Task ConnectAsync()
        {
            _status = DeviceStatus.Connecting;

            var rm = new ResourceManager();
            _session = (MessageBasedSession)rm.Open(_resource);

            _session.RawIO.Write("*IDN?\n");
            string idn = _session.RawIO.ReadString();

            _session.RawIO.Write("CURR:RANG?\n");
            string x = _session.RawIO.ReadString();

            _name = ParseName(idn);

            _status = DeviceStatus.Connected;

            await Task.CompletedTask;
        }

        private string ParseName(string idn)
        {
            var parts = idn.Split(',');
            if (parts.Length >= 2)
                return $"{parts[0]} {parts[1]}";

            return idn.Trim();
        }


        public async Task DisconnectAsync()
        {
            _session?.Dispose();
            _session = null;
            await Task.CompletedTask;
        }

        public async Task<MeasurementResult> MeasureAsync(MeasurementParameters parameters)
        {
            if (_session == null)
                throw new InvalidOperationException("Device not connected.");

            try
            {
                await SetRangeAsync(parameters.Type,
                    new MeasurementRange { MaxValue = parameters.Range });

                await Task.Delay(parameters.SettlingTime);

                string command = GetMeasureCommand(parameters.Type);

                _session.RawIO.Write(command + "\n");
                string response = _session.RawIO.ReadString();

                double value = double.Parse(response.Trim(),
                    CultureInfo.InvariantCulture);

                return new MeasurementResult
                {
                    Value = value,
                    Unit = parameters.Unit,
                    Timestamp = DateTime.UtcNow,
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                return new MeasurementResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<IEnumerable<MeasurementRange>> GetAvailableRangesAsync(MeasurementType type)
        {
            // Example static ranges – adjust to device spec
            var ranges = new List<MeasurementRange>();

            switch (type)
            {
                case MeasurementType.VoltageDC:
                    ranges.Add(new MeasurementRange { Name = "10V", MinValue = -10, MaxValue = 10, Resolution = 0.0001 });
                    ranges.Add(new MeasurementRange { Name = "100V", MinValue = -100, MaxValue = 100, Resolution = 0.001 });
                    break;

                case MeasurementType.Resistance:
                    ranges.Add(new MeasurementRange { Name = "1kΩ", MinValue = 0, MaxValue = 1000, Resolution = 0.1 });
                    ranges.Add(new MeasurementRange { Name = "10kΩ", MinValue = 0, MaxValue = 10000, Resolution = 1 });
                    break;

                default:
                    break;
            }

            return await Task.FromResult(ranges);
        }

        public async Task SetRangeAsync(MeasurementType type, MeasurementRange range)
        {
            if (_session == null)
                throw new InvalidOperationException("Device not connected.");

            string command = GetConfigCommand(type, range.MaxValue);

            _session.RawIO.Write(command + "\n");

            await Task.CompletedTask;
        }

        private string GetMeasureCommand(MeasurementType type)
        {
            return type switch
            {
                MeasurementType.VoltageDC => "MEAS:VOLT:DC?",
                MeasurementType.VoltageAC => "MEAS:VOLT:AC?",
                MeasurementType.CurrentDC => "MEAS:CURR:DC?",
                MeasurementType.CurrentAC => "MEAS:CURR:AC?",
                MeasurementType.Resistance => "MEAS:RES?",
                MeasurementType.Frequency => "MEAS:FREQ?",
                MeasurementType.Temperature => "MEAS:TEMP?",
                MeasurementType.Capacitance => "MEAS:CAP?",
                _ => throw new NotSupportedException()
            };
        }

        private string GetConfigCommand(MeasurementType type, double range)
        {
            return type switch
            {
                MeasurementType.VoltageDC => $"VOLT:RANG {range}",
                MeasurementType.VoltageAC => $"CONF:VOLT:AC {range}",
                MeasurementType.CurrentDC => $"CURR:RANG {range}",
                MeasurementType.CurrentAC => $"CONF:CURR:AC {range}",
                MeasurementType.Resistance => $"CONF:RES {range}",
                MeasurementType.Frequency => "CONF:FREQ",
                MeasurementType.Temperature => "CONF:TEMP",
                MeasurementType.Capacitance => "CONF:CAP",
                _ => throw new NotSupportedException()
            };
        }

        public void Dispose()
        {
            _session?.Dispose();
        }

        Task<bool> IDevice.ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task ResetAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetIdentificationAsync()
        {
            throw new NotImplementedException();
        }
    }