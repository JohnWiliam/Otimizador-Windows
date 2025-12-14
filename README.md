# System Optimizer (C# / WPF Edition)

This is a modern rewrite of the "Otimizador v2" script, converted into a native C# WPF application using the `WPF-UI` library.

## Features

- **Modern UI**: Uses Mica/Acrylic effects and Fluent Design (Windows 11 style).
- **Portable**: Compiles to a single `.exe` file with no external dependencies (Self-Contained).
- **Categories**:
  - **Privacy**: Disable Telemetry, Cortana, Ad ID, etc.
  - **Performance**: Ultimate Power Plan, SysMain, GameDVR, etc.
  - **Network**: TCP Optimization, CUBIC, ECN.
  - **Security**: Extensions, AutoRun.
  - **Appearance**: Transparency, Dark Mode.
  - **Cleanup**: Clean Temp, Prefetch, DNS Cache.

## How to Build

### Prerequisites
- Windows 10 or 11.
- **.NET 8 SDK** (Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)).

### Steps
1. Open PowerShell in this folder.
2. Run the build script:
   ```powershell
   .\build.ps1
   ```
3. The executable will be created in the `Build` folder.

## Running
- Right-click `SystemOptimizer.exe` and select **Run as Administrator** (Required for Registry and Service changes).

## Notes
- This application modifies system settings (Registry, Services). 
- Use the "Revert" buttons to restore default settings if needed.
- The Cleanup tool deletes temporary files permanently.
