# CalibrationApp

A .NET-based desktop application for automated calibration of test and measurement equipment.

## Overview

CalibrationApp provides a unified interface for communicating with, controlling, and calibrating various measurement devices including multimeters, calibrators, relay switches, and signal generators.

## Architecture

The solution consists of multiple projects:

```
CalibrationApp/
├── CalibriCore/          # Core models and business logic
├── CalibriConsole/       # Console interface for testing
├── CalibriWpf/           # Main WPF desktop application
├── CalibriDevices/       # Device drivers and communications
├── CalibriTests/         # Unit tests
├── CalibriSandBox/       # Experimental features
├── NetworkScanner/       # Network device discovery
└── Documentation/        # Device-specific documentation
```

## Supported Devices

### Real Devices

| Device | Type | Description |
|--------|------|-------------|
| EA-ELM5080-25 | Electronic Load | DC electronic load |
| EAPS8720U | Power Supply | Programmable power supply |
| H&H ZS Lasten | Measurement Device | H&H measurement device |
| Keithley 3706A | Switch System | High-density switch system |
| MicroChip Relay Card | Relay Controller | Microchip-based relay card |

### Documentation

Device-specific documentation is available in the `Documentation/` folder:
- `Calibrator_Meatest9010+d/` - Meatest 9010+d calibrator
- `H&H ZS Lasten/` - H&H measurement device documentation
- `Keithley 3706A/` - Keithley switch system documentation

## Features

- **Network Scanning**: Automatically discover devices on the local subnet
- **Device Communication**: SCPI-based communication with measurement instruments
- **Recipe-based Calibration**: Define calibration procedures using the Recipe model
- **Logging**: Comprehensive logging for debugging and audit trails
- **WPF Interface**: Modern Windows desktop interface
- **Console Tools**: Command-line utilities for automation

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Windows 10/11

### Building

```bash
dotnet build Calibri.sln
```

### Running

```bash
# Console application
dotnet run --project CalibriConsole

# WPF Application
dotnet run --project CalibriWpf
```

### Network Scanning

The console app includes network scanning capabilities:

```csharp
var scanner = new NetworkScanner();
var devices = await scanner.ScanLocalSubnetAsync();
```

## Device Communication

Devices communicate via SCPI (Standard Commands for Programmable Instruments). Each device driver implements the `IDevice` interface defined in `CalibriDevices/Interfaces/`.

### Example: Using a Device

```csharp
using CalibriDevices.Devices;

var device = new Keithley3706A("192.168.1.100");
await device.ConnectAsync();
var reading = await device.ReadAsync();
```

## Configuration

Configuration files are stored in `CalibriWpf/Configs/`. The application uses JSON-based configuration for:
- Device connections
- Calibration parameters
- User preferences

## Logging

Logging is handled via `CalibriDevices/Logging/` and `CalibriWpf/Logging/`. Logs are written to:
- Console (debug builds)
- File system (release builds)

## Development

### Adding a New Device

1. Create a new class in `CalibriDevices/Devices/Real/`
2. Implement the `IDevice` interface
3. Add device documentation in `Documentation/`
4. Register the device in `DeviceFactory.cs`

### Running Tests

```bash
dotnet test CalibriTests
```

## License

Private repository - All rights reserved
