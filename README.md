# Prospect Server Overlay

A Windows overlay application that monitors the Prospect game log file and displays current server information including region, server address, and session ID.

## Solution Structure

```
Prospect-Server-Overlay/
├── ProspectServerOverlay/           # Main application project
│   ├── MainWindow.xaml              # Overlay UI layout (XAML)
│   ├── MainWindow.xaml.cs           # Main application logic
│   ├── App.xaml                     # WPF application entry point
│   ├── App.xaml.cs                  # Application startup logic
│   ├── AssemblyInfo.cs              # Assembly metadata
│   ├── appsettings.json             # Configuration file
│   ├── ProspectServerOverlay.csproj # .NET project file
│   ├── README.md                    # Project-specific documentation
│   ├── bin/                         # Build output (Debug/Release)
│   └── obj/                         # Build intermediate files
├── installer.iss                    # Inno Setup installer script
├── build_release.bat                # Build script for release
├── create_portable_zip.bat          # Script to create portable package
├── .gitignore                       # Git ignore rules
└── README.md                        # Main project documentation
```

## Quick Start

### Prerequisites
- Windows 10/11
- .NET 9.0 Runtime
- Prospect game installed

### Development
```bash
# Clone the repository
git clone <repository-url>
cd Prospect-Server-Overlay

# Build the project
dotnet build ProspectServerOverlay/ProspectServerOverlay.csproj

# Run the application
dotnet run --project ProspectServerOverlay/ProspectServerOverlay.csproj
```

### Release Build
```bash
# Create release build
build_release.bat

# Create portable package
create_portable_zip.bat

# Create installer (requires Inno Setup)
# Use installer.iss with Inno Setup
```

### Configuration
Edit `ProspectServerOverlay/appsettings.json` to customize:
- Overlay position and opacity
- Log file path
- Update intervals
- Debug settings
- Text size

The application automatically monitors the Prospect log file and displays server information in a transparent overlay window.

## Features

- **Real-time Server Monitoring**: Automatically detects when you connect to a new server by monitoring the Prospect log file
- **Always-on-top Overlay**: Transparent overlay window that stays on top of all applications, including fullscreen games
- **Server Information Display**:
  - Current region (e.g., WestUs, EastUs)
  - Session ID
  - Status indicator showing monitoring state
  - Debug information toggle
- **Configurable**: Customizable via `appsettings.json` for position, opacity, text size, and behavior
- **Performance Optimized**: Minimal impact on gaming performance

## How to Use

1. **Launch the Application**: Run `ProspectServerOverlay.exe` before or while playing Prospect
2. **Automatic Detection**: The overlay will automatically appear and begin monitoring the log file
3. **Server Connection**: When you connect to a server in Prospect, the overlay will update with current server information
4. **Settings Access**: Use the configurable hotkey (default: Ctrl+Q) to open settings dialog
5. **Close Application**: Use the red close button in the settings dialog to exit

The overlay monitors the log file at: `%LOCALAPPDATA%\Prospect\Saved\Logs\Prospect.log`

## License

This project is provided as-is for educational and personal use.
