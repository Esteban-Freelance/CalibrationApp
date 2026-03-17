namespace CalibrationDevices.Devices.Real;

    using CalibrationDevices.Interfaces;
using CalibrationDevices.Logging;
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
    private string _name = "H&H Load";
    private DeviceStatus _status = DeviceStatus.Disconnected;
    private ILogService _log;

    public string Id => _id;
    public string Name => _name;
    public string DeviceType => "Load";
    public DeviceStatus Status => _status;

    public HhMeasurementDevice(string id, string resource, ILogService logService = null)
    {
        _id = id;
        _resource = resource;
        _log = logService ?? NullLogService.Instance;
    }

    public async Task<bool> ConnectAsync()
    {
        if (_status == DeviceStatus.Connected)
            return true;

        _status = DeviceStatus.Connecting;
        _log.Debug($"Connecting toH&H Load at {_resource}...");

        try
        {
            _status = DeviceStatus.Connecting;

            var rm = new ResourceManager();
            _session = (MessageBasedSession)rm.Open(_resource);

            _session.RawIO.Write("*IDN?\n");
            var idn = _session.RawIO.ReadString();
            await ResetAsync();

            _name = ParseName(idn);
            _status = DeviceStatus.Connected;
            return true;
        }
        catch (Exception ex) 
        {
            _log.Error($"Connection failed {ex.Message}", Name);
            _status = DeviceStatus.Error;
            return false;
        }

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

    private string Query(string query)
    {
        _session.RawIO.Write(query + "\n");
        return _session.RawIO.ReadString();
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

            case MeasurementType.CurrentDC:
                ranges.Add(new MeasurementRange { Name = "100mA", MinValue = -0.1, MaxValue = 0.1, Resolution = 0.000001 });
                ranges.Add(new MeasurementRange { Name = "1A", MinValue = -1, MaxValue = 1, Resolution = 0.00001 });
                break;

            case MeasurementType.CurrentShunt:
                ranges.Add(new MeasurementRange { Name = "Auto", MinValue = 0, MaxValue = 10, Resolution = 0.000001 });
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
        await Task.Delay(2500);

        await SetInputOn(type);
        await Task.Delay(2500);

        await Task.CompletedTask;
    }

    public async Task SetInputOn(MeasurementType type)
    {
        switch (type)
        {
            case MeasurementType.VoltageDC:
                return;

            case MeasurementType.CurrentShunt:
            case MeasurementType.CurrentDC:
                SetInput();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private void SetInput()
    {
        _session.RawIO.Write("CURR:RANG 12");
        _session.RawIO.Write("CURR MAX");
        _session.RawIO.Write("INput ON");
    }

    private string GetMeasureCommand(MeasurementType type)
    {
        return type switch
        {
            MeasurementType.VoltageDC => "MEAS:VOLT:DC?",
            MeasurementType.CurrentDC => "MEAS:CURR:DC?",
            MeasurementType.CurrentShunt => "MEAS:CURR:DC?",
            _ => throw new NotSupportedException()
        };
    }

    private string GetConfigCommand(MeasurementType type, double range)
    {
        return type switch
        {
            MeasurementType.VoltageDC => $"VOLT:RANG {range.ToString(CultureInfo.InvariantCulture)}",
            MeasurementType.CurrentDC => $"CURR:RANG {range.ToString(CultureInfo.InvariantCulture)}",
            MeasurementType.CurrentShunt => $"CURR:RANG {range.ToString(CultureInfo.InvariantCulture)}",
            _ => throw new NotSupportedException()
        };
    }

    public void Dispose()
    {
        _session?.Dispose();
    }

    public async Task ResetAsync()
    {
        _session.RawIO.Write("*RST");
        await Task.Delay(2500);
    }

    public Task<string> GetIdentificationAsync()
    {
        throw new NotImplementedException();
    }
}