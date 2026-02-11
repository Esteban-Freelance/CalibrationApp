using System.Xml.Serialization;

namespace CalibrationApp.Models;

[XmlRoot("Device")]
public class DeviceConfig
{
    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;
    
    [XmlElement("Type")]
    public string Type { get; set; } = string.Empty;
    
    [XmlElement("Driver")]
    public string Driver { get; set; } = string.Empty;
    
    [XmlElement("Name")]
    public string Name { get; set; } = string.Empty;
    
    [XmlElement("Connection")]
    public ConnectionConfig Connection { get; set; } = new();
}

public class ConnectionConfig
{
    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;
    
    [XmlElement("Address")]
    public string? Address { get; set; }
    
    [XmlElement("Port")]
    public string? Port { get; set; }
    
    [XmlElement("BaudRate")]
    public int? BaudRate { get; set; }
    
    [XmlElement("Board")]
    public int? Board { get; set; }
    
    [XmlElement("Timeout")]
    public int TimeoutMs { get; set; } = 5000;
}

[XmlRoot("TestBench")]
public class TestBenchConfig
{
    [XmlAttribute("id")]
    public string Id { get; set; } = string.Empty;
    
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [XmlArray("Devices")]
    [XmlArrayItem("Device")]
    public List<DeviceReference> Devices { get; set; } = new();
    
    [XmlArray("Wiring")]
    [XmlArrayItem("Route")]
    public List<WiringRoute> Wiring { get; set; } = new();
}

public class DeviceReference
{
    [XmlAttribute("ref")]
    public string DeviceId { get; set; } = string.Empty;
    
    [XmlAttribute("role")]
    public string Role { get; set; } = string.Empty;
}

public class WiringRoute
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [XmlAttribute("switchChannel")]
    public int SwitchChannel { get; set; }
    
    [XmlAttribute("description")]
    public string Description { get; set; } = string.Empty;
}
