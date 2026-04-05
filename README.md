# USB Temperature Monitor

A lightweight Windows desktop app designed for the [USBTemp](https://usbtemp.com) USB thermometer sensor. Built with Claude Code.

Reads temperature from the DS18B20 sensor via the USB-to-serial adapter.

![screenshot](https://github.com/user-attachments/assets/placeholder.png)

## Features

- Real-time temperature display (Fahrenheit or Celsius)
- Auto-detects the sensor across all COM ports, or manually select a port
- Native Windows UI with menu bar and status bar
- Minimal resource usage (~10-15 MB RAM)

## How it works

The DS18B20 communicates over the 1-Wire protocol. This app bit-bangs 1-Wire over a standard UART serial adapter by switching between 9600 baud (reset pulse) and 115200 baud (data).

Temperature is read every 10 seconds. The 9-byte scratchpad response is validated with CRC-8 before display.

## Requirements

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (for building)
- A USB-to-serial adapter wired to a DS18B20 sensor

## Build and run

```
dotnet run
```

## Publish a single exe

```
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true
```

The output exe is in `bin\Release\net10.0-windows\win-x64\publish\`.

## Credits

App icon from Apple's [SF Symbols](https://developer.apple.com/sf-symbols/).

## Project structure

| File | Description |
|---|---|
| `Program.cs` | Entry point |
| `MainForm.cs` | WinForms UI -- menu bar, temperature label, status bar |
| `Thermometer.cs` | DS18B20 driver -- 1-Wire over UART serial protocol |
| `UsbTempMonitor.csproj` | .NET project file |
