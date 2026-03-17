// See https://aka.ms/new-console-template for more information

using CalibrationDevices.Devices.Real;
using CalibrationDevices.Interfaces;


Console.WriteLine("Hi, Calibri");
Console.WriteLine("Scanning Network");

Console.WriteLine("Keythley3706A");
var keythleyConfig = new TcpConfig
{
    IpAddress = "192.168.178.20",
    Port = 5025,
    TimeoutMs = 5000
};

var keythley = new Keithley3706A("Keithley01", "MyKeithley", keythleyConfig);
var parameters = new MeasurementParameters
{
    Type = MeasurementType.VoltageDC,
    Range = 100,
    SampleCount = 2,
    Unit = "V",
    Channel = "1001"
};

var shuntCurrentParams = new MeasurementParameters
{
    Type = MeasurementType.CurrentShunt,
    ShuntResistance = 100,
    Range = 100,
    SampleCount = 2,
    Unit = "A",
    Channel = "1001"
};


bool v = await keythley.ConnectAsync();



var VoltageMeasurement = await keythley.MeasureAsync(parameters);
Console.WriteLine($"{VoltageMeasurement.Value} {VoltageMeasurement.Unit}");


var CurrentMeasurement = await keythley.MeasureAsync(shuntCurrentParams);
Console.WriteLine($"{CurrentMeasurement.Value} {CurrentMeasurement.Unit}");

//var id = keythley.GetIdentificationAsync().Result;



//var config = new RelayCardConfig 
//{
//    IpAddress = "192.168.178.20",

//};

//var card = new MicrochipRelayCard(id: "01", name: "MicrochipCard", config: config);
//var statid = await card.ConnectAsync();
//var currentChannel = card.GetCurrentChannelAsync().Result;   
//await card.SetChannelAsync("ON");
//await Task.Delay(2000);
//await card.SetChannelAsync("OFF");
//await Task.Delay(2000);
//await card.SetChannelAsync("ON");
//await Task.Delay(2000);
//await card.SetChannelAsync("OFF");
//await Task.Delay(2000);

//var EASTcpConfig = new TcpConfig
//{
//    IpAddress = "192.168.178.22",
//    Port = 1001,
//    TimeoutMs = 5000
//};

//var ea = new EAPS8720U(id: "EA-01", name: "EA-SomeName", config: EASTcpConfig);

Console.ReadLine();
