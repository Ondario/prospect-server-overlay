using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text.Json;

namespace ProspectServerOverlay;

/// <summary>
/// Simple file logger for debugging events - optimized for performance
/// </summary>
public static class DebugLogger
{
    private static readonly string _logFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
    private static bool _debugEnabled = false;
    private static DateTime _lastTimerLog = DateTime.MinValue;

    static DebugLogger()
    {
        // Clear log file on startup
        try
        {
            File.WriteAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Application started\n");
        }
        catch { }
    }

    public static void SetDebugEnabled(bool enabled)
    {
        _debugEnabled = enabled;
    }

    public static void Log(string message)
    {
        if (!_debugEnabled) return;

        try
        {
            File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    public static void LogTimer(string message)
    {
        if (!_debugEnabled) return;

        // Only log timer events every 30 seconds to reduce I/O
        if ((DateTime.Now - _lastTimerLog).TotalSeconds < 30) return;

        _lastTimerLog = DateTime.Now;
        Log(message);
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var errorMsg = $"ERROR: {message}";
        if (ex != null)
            errorMsg += $" | Exception: {ex.Message}";
        // Always log errors regardless of debug setting
        try
        {
            File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {errorMsg}\n");
        }
        catch { }
    }
}

/// <summary>
/// Windows API functions for forcing window to stay on top and global hotkeys
/// </summary>
public static class WindowUtils
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    // Global hotkey functions
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Windows message constants
    public const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 1;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_TOP = new IntPtr(0);

    // Hotkey modifiers
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_Q = 0x51; // Q key

    private static IntPtr _registeredWindowHandle = IntPtr.Zero;

    /// <summary>
    /// Forces the window to stay on top, even above fullscreen applications
    /// </summary>
    public static void ForceTopMost(IntPtr hWnd)
    {
        // Use SWP_NOACTIVATE to prevent stealing focus from the game
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>
    /// Checks if the foreground window is a fullscreen application
    /// </summary>
    public static bool IsFullscreenAppRunning()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        // Simple heuristic: if foreground window exists and is valid, assume it might be fullscreen
        return IsWindow(foreground);
    }

    /// <summary>
    /// Registers a global hotkey for the specified window using the configured hotkey
    /// </summary>
    public static bool RegisterGlobalHotkey(IntPtr hWnd, string hotkeyString)
    {
        _registeredWindowHandle = hWnd;

        // Parse the hotkey string
        var parsedHotkey = ParseHotkeyString(hotkeyString);
        if (!parsedHotkey.HasValue)
        {
            Console.WriteLine($"Failed to parse hotkey string: {hotkeyString}");
            return false;
        }

        var (modifier, keyCode) = parsedHotkey.Value;
        return RegisterHotKey(hWnd, HOTKEY_ID, modifier, keyCode);
    }

    /// <summary>
    /// Unregisters the global hotkey
    /// </summary>
    public static void UnregisterGlobalHotkey()
    {
        if (_registeredWindowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_registeredWindowHandle, HOTKEY_ID);
            _registeredWindowHandle = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Checks if a Windows message is our registered hotkey
    /// </summary>
    public static bool IsHotkeyMessage(int msg, IntPtr wParam)
    {
        return msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID;
    }

    /// <summary>
    /// Parses a hotkey string like "Ctrl+Q" and returns the modifier and key code
    /// </summary>
    public static (uint modifier, uint keyCode)? ParseHotkeyString(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return null;

        var parts = hotkeyString.Split('+');
        if (parts.Length != 2)
            return null;

        var modifier = parts[0].Trim().ToUpper();
        var key = parts[1].Trim().ToUpper();

        uint modifierCode = 0;
        if (modifier == "CTRL" || modifier == "CONTROL")
            modifierCode = MOD_CONTROL;
        else if (modifier == "ALT")
            modifierCode = 0x0001; // MOD_ALT
        else if (modifier == "SHIFT")
            modifierCode = 0x0004; // MOD_SHIFT
        else if (modifier == "WIN" || modifier == "WINDOWS")
            modifierCode = 0x0008; // MOD_WIN
        else
            return null;

        uint keyCode = key.Length == 1 ? (uint)key[0] : 0;
        if (keyCode == 0)
        {
            // Handle special keys
            switch (key)
            {
                case "F1": keyCode = 0x70; break;
                case "F2": keyCode = 0x71; break;
                case "F3": keyCode = 0x72; break;
                case "F4": keyCode = 0x73; break;
                case "F5": keyCode = 0x74; break;
                case "F6": keyCode = 0x75; break;
                case "F7": keyCode = 0x76; break;
                case "F8": keyCode = 0x77; break;
                case "F9": keyCode = 0x78; break;
                case "F10": keyCode = 0x79; break;
                case "F11": keyCode = 0x7A; break;
                case "F12": keyCode = 0x7B; break;
                default: return null;
            }
        }

        return (modifierCode, keyCode);
    }
}

/// <summary>
/// Server information extracted from Prospect logs
/// </summary>
public class ServerInfo : INotifyPropertyChanged
{
    private string _region = "Monitoring logs...";
    private string _serverId = "Monitoring logs...";
    private string _sessionId = "Monitoring logs...";
    private string _serverAddress = "Monitoring logs...";
    private string _status = "Initializing...";
    private string _debugInfo = "";
    private bool _debugVisible = false;

    public string Region
    {
        get => _region;
        set { _region = value; OnPropertyChanged(); }
    }

    public string ServerId
    {
        get => _serverId;
        set { _serverId = value; OnPropertyChanged(); }
    }

    public string SessionId
    {
        get => _sessionId;
        set { _sessionId = value; OnPropertyChanged(); }
    }

    public string ServerAddress
    {
        get => _serverAddress;
        set { _serverAddress = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string DebugInfo
    {
        get => _debugInfo;
        set { _debugInfo = value; OnPropertyChanged(); }
    }

    public bool DebugVisible
    {
        get => _debugVisible;
        set { _debugVisible = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ServerInfo _serverInfo;
    private FileSystemWatcher? _fileWatcher;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _topmostTimer;
    private string _logFilePath = string.Empty;
    private readonly IConfiguration _configuration;
    private Window? _settingsWindow; // Track the current settings window
    private string _settingsHotkey = "Ctrl+Q";
    private bool _isCapturingHotkey = false;
    private DateTime _lastFileReadTime = DateTime.MinValue; // Cache file state to avoid unnecessary reads
    private (string region, string serverId, string sessionId, string serverAddress)? _cachedServerInfo;

    public MainWindow()
    {
        DebugLogger.Log("MainWindow constructor called");

        // Load configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        _configuration = builder.Build();

        DebugLogger.Log("Configuration loaded");

        InitializeComponent();

        _serverInfo = new ServerInfo();
        DataContext = _serverInfo;

        // Apply configuration to window
        ApplyConfiguration();

        // Set up timer for periodic checks
        var updateInterval = _configuration.GetValue<int>("OverlaySettings:UpdateIntervalSeconds", 5);
        _updateTimer = new DispatcherTimer();
        _updateTimer.Interval = TimeSpan.FromSeconds(updateInterval);
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();

        DebugLogger.Log($"Timer started with {updateInterval} second interval");

        // Set up timer to force window to stay on top (every 2 seconds for better responsiveness)
        _topmostTimer = new DispatcherTimer();
        _topmostTimer.Interval = TimeSpan.FromSeconds(2);
        _topmostTimer.Tick += TopmostTimer_Tick;
        _topmostTimer.Start();

        DebugLogger.Log("Topmost timer started (2 second interval)");

        // Global hotkey will be registered in OnSourceInitialized when window handle is ready

        this.Activated += MainWindow_Activated; // Prevent focus stealing
        this.Deactivated += MainWindow_Deactivated; // Ensure immediate topmost restoration
        this.Loaded += MainWindow_Loaded; // Ensure topmost when window first loads

        // Set up file watcher if log file exists
        SetupFileWatcher();

        // Initial update
        DebugLogger.Log("Calling initial UpdateServerInfo");
        UpdateServerInfo();
    }

    private void ApplyConfiguration()
    {
        DebugLogger.Log("ApplyConfiguration called");

        // Set window position
        var left = _configuration.GetValue<double>("OverlaySettings:WindowPosition:Left", 20);
        var top = _configuration.GetValue<double>("OverlaySettings:WindowPosition:Top", 20);
        Left = left;
        Top = top;
        DebugLogger.Log($"Window position set to ({left}, {top})");

        // Set opacity
        var opacity = _configuration.GetValue<double>("OverlaySettings:Opacity", 0.9);
        this.Opacity = opacity;
        DebugLogger.Log($"Window opacity set to {opacity}");

        // Set debug visibility
        var debugVisible = _configuration.GetValue<bool>("OverlaySettings:DebugVisible", false);
        _serverInfo.DebugVisible = debugVisible;
        DebugLogger.SetDebugEnabled(debugVisible);
        DebugLogger.Log($"Debug visibility set to {debugVisible}");

        // Set settings hotkey
        _settingsHotkey = _configuration.GetValue<string>("OverlaySettings:SettingsHotkey", "Ctrl+Q");
        DebugLogger.Log($"Settings hotkey set to {_settingsHotkey}");

        // Get log file path from config or default
        var configLogPath = _configuration.GetValue<string>("LogSettings:LogFilePath", "");
        if (!string.IsNullOrEmpty(configLogPath))
        {
            // Expand environment variables
            var expandedPath = Environment.ExpandEnvironmentVariables(configLogPath);
            _serverInfo.DebugInfo = $"Using config path: {configLogPath} -> {expandedPath}";
            _logFilePath = expandedPath;
            DebugLogger.Log($"Using configured log path: {configLogPath} -> {expandedPath}");
        }
        else
        {
            // Default path
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _logFilePath = System.IO.Path.Combine(localAppData, "Prospect", "Saved", "Logs", "Prospect.log");
            _serverInfo.DebugInfo = $"Using default path: {_logFilePath}";
            DebugLogger.Log($"Using default log path: {_logFilePath}");
        }
    }

    private void SetupFileWatcher()
    {
        try
        {
            string? logDirectory = System.IO.Path.GetDirectoryName(_logFilePath);
            if (logDirectory != null && Directory.Exists(logDirectory))
            {
                _fileWatcher = new FileSystemWatcher(logDirectory, "Prospect.log");
                _fileWatcher.Changed += OnLogFileChanged;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            // Log file watching setup failed, will rely on timer
            System.Diagnostics.Debug.WriteLine($"Failed to setup file watcher: {ex.Message}");
        }
    }

    private DispatcherTimer? _fileChangeTimer;

    private void OnLogFileChanged(object sender, FileSystemEventArgs e)
    {
        DebugLogger.Log($"File watcher detected change: {e.ChangeType} on {e.FullPath}");
        // Delay the update slightly to allow file to be fully written
        // Use a timer instead of Thread.Sleep to avoid blocking threads
        if (_fileChangeTimer == null)
        {
            _fileChangeTimer = new DispatcherTimer();
            _fileChangeTimer.Interval = TimeSpan.FromMilliseconds(100);
            _fileChangeTimer.Tick += (s, args) =>
            {
                _fileChangeTimer.Stop();
                _fileChangeTimer = null;
                DebugLogger.Log("File watcher triggering UpdateServerInfo after delay");
                UpdateServerInfo();
            };
        }
        // Reset the timer if it's already running (debounce rapid changes)
        _fileChangeTimer.Stop();
        _fileChangeTimer.Start();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        DebugLogger.LogTimer("Timer tick - automatic UpdateServerInfo");
        UpdateServerInfo();
    }

    private void TopmostTimer_Tick(object? sender, EventArgs e)
    {
        // Periodic maintenance - force window to stay on top
        // Use normal interval but ensure it works
        ForceTopMostImmediately();
    }


    private void MainWindow_Activated(object sender, EventArgs e)
    {
        // Prevent this window from stealing focus from games
        // When activated, immediately return focus to the previously active window
        ForceTopMostImmediately();
    }

    private void MainWindow_Deactivated(object sender, EventArgs e)
    {
        // When window loses focus (e.g., user tabs out of game), immediately restore topmost status
        // This prevents the delay that was causing the overlay to disappear
        ForceTopMostImmediately();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Ensure window is topmost immediately when it first loads
        ForceTopMostImmediately();
    }

    private void ForceTopMostImmediately()
    {
        ForceTopMostAggressively();
    }

    private void ForceTopMostAggressively()
    {
        // Only apply aggressive topmost to the main overlay window
        if (this.Title != "Prospect Server Overlay") return;

        try
        {
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // Force topmost multiple times immediately to handle timing issues
            for (int i = 0; i < 3; i++)
            {
                WindowUtils.ForceTopMost(windowHandle);
                System.Threading.Thread.Sleep(5); // Very small delay between calls
            }

            DebugLogger.LogTimer("Aggressive topmost force applied to main window");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Error in ForceTopMostAggressively", ex);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            // Get the window handle
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            DebugLogger.Log($"Window handle obtained: {windowHandle}");

            // Register global hotkey
            if (WindowUtils.RegisterGlobalHotkey(windowHandle, _settingsHotkey))
            {
                DebugLogger.Log($"Global hotkey ({_settingsHotkey}) registered successfully");
            }
            else
            {
                var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                DebugLogger.LogError($"Failed to register global hotkey ({_settingsHotkey}). Win32 Error: {error}", null);
            }

            // Use ComponentDispatcher for more reliable message handling
            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
            DebugLogger.Log("ComponentDispatcher message filter installed");
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Error in OnSourceInitialized", ex);
        }
    }

    private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        // Debug: Log WM_HOTKEY messages
        if (msg.message == 0x0312) // WM_HOTKEY
        {
            DebugLogger.Log($"WM_HOTKEY received via ComponentDispatcher: msg={msg.message}, wParam={msg.wParam}, lParam={msg.lParam}");
        }

        // Check if this is our registered hotkey message
        if (WindowUtils.IsHotkeyMessage(msg.message, msg.wParam))
        {
            // Don't process hotkey if we're currently capturing a new hotkey
            if (_isCapturingHotkey)
            {
                DebugLogger.Log("Hotkey pressed while capturing - ignoring to allow capture");
                return;
            }

            DebugLogger.Log("Global hotkey pressed - opening settings");
            handled = true;
            Dispatcher.Invoke(() => ToggleSettingsDialog());
        }
    }

    private void ToggleSettingsDialog()
    {
        if (_settingsWindow != null)
        {
            // Settings window is open, close it
            DebugLogger.Log("Closing existing settings window");
            _settingsWindow.Close();
            _settingsWindow = null;
        }
        else
        {
            // Settings window is closed, open it
            DebugLogger.Log("Opening new settings window");
            ShowSettingsDialog();
        }
    }

    // Windows API functions for getting key state
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt key
    private const int VK_SHIFT = 0x10;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    /// <summary>
    /// Captures a key combination from a KeyEventArgs and returns it as a string
    /// </summary>
    private string CaptureHotkey(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key;

        // Handle special keys
        if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
        {
            return ""; // Don't capture modifier keys alone
        }

        // Use Windows API to check modifier key states
        bool ctrlPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        bool altPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        bool shiftPressed = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        bool winPressed = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;

        string modifierString = "";
        if (ctrlPressed)
            modifierString = "Ctrl";
        else if (altPressed)
            modifierString = "Alt";
        else if (shiftPressed)
            modifierString = "Shift";
        else if (winPressed)
            modifierString = "Win";

        if (string.IsNullOrEmpty(modifierString))
            return ""; // Require a modifier key

        string keyString;
        if (key >= System.Windows.Input.Key.F1 && key <= System.Windows.Input.Key.F12)
        {
            keyString = key.ToString(); // F1, F2, etc.
        }
        else if (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
        {
            keyString = key.ToString(); // A, B, C, etc.
        }
        else if (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
        {
            keyString = ((char)('0' + (key - System.Windows.Input.Key.D0))).ToString(); // 0, 1, 2, etc.
        }
        else
        {
            return ""; // Unsupported key
        }

        return $"{modifierString}+{keyString}";
    }

    private void UpdateServerInfo()
    {
        DebugLogger.Log("UpdateServerInfo called");

        try
        {
            _serverInfo.Status = $"Monitoring: {_logFilePath}";
            DebugLogger.Log($"Status updated to: Monitoring: {_logFilePath}");

            if (!File.Exists(_logFilePath))
            {
                DebugLogger.Log($"ERROR: Log file does not exist: {_logFilePath}");
                _serverInfo.Status = $"Log file not found: {_logFilePath}";
                _serverInfo.Region = "Waiting for log file";
                _serverInfo.ServerId = "Waiting for log file";
                _serverInfo.SessionId = "Waiting for log file";
                _serverInfo.ServerAddress = "Waiting for log file";
                return;
            }

            // Check if file has been modified since last read
            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.LastWriteTime <= _lastFileReadTime && _cachedServerInfo.HasValue)
            {
                // File hasn't changed, use cached data
                DebugLogger.Log("File unchanged, using cached server info");
                UpdateUIWithCachedServerInfo(_cachedServerInfo.Value);
                return;
            }

            DebugLogger.Log($"Log file exists: {_logFilePath}");
            _serverInfo.Status = $"Reading log file...";
            _lastFileReadTime = fileInfo.LastWriteTime;
            var serverData = ParseLatestServerInfo(_logFilePath);
            _cachedServerInfo = serverData;
            if (serverData != null)
            {
                DebugLogger.Log($"SUCCESS: Found server data - Region: {serverData.Value.region}, Server: {serverData.Value.serverAddress}");
                _serverInfo.Status = $"Connected to {serverData.Value.region}";
                _serverInfo.Region = serverData.Value.region;
                _serverInfo.ServerId = serverData.Value.serverId;
                _serverInfo.SessionId = serverData.Value.sessionId;
                _serverInfo.ServerAddress = serverData.Value.serverAddress;
            }
            else
            {
                // Check if this is a file locking issue vs. no data found
                if (_serverInfo.DebugInfo.Contains("Log file locked"))
                {
                    DebugLogger.Log("File locking detected - will retry on next update");
                    _serverInfo.Status = "Waiting for log file access...";
                    // Keep existing values displayed
                }
                else
                {
                    DebugLogger.Log("No server connection data found in logs");
                    _serverInfo.Status = "No server connection found in logs";
                    _serverInfo.Region = "Waiting for match...";
                    _serverInfo.ServerId = "Waiting for match...";
                    _serverInfo.SessionId = "Waiting for match...";
                    _serverInfo.ServerAddress = "Waiting for match...";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Exception in UpdateServerInfo", ex);
            _serverInfo.Status = $"Error: {ex.Message}";
            _serverInfo.Region = "Error reading logs";
            _serverInfo.ServerId = "Error reading logs";
            _serverInfo.SessionId = "Error reading logs";
            _serverInfo.ServerAddress = ex.Message;
        }
    }

    private void UpdateUIWithCachedServerInfo((string region, string serverId, string sessionId, string serverAddress) serverInfo)
    {
        _serverInfo.Region = serverInfo.region;
        _serverInfo.ServerId = serverInfo.serverId;
        _serverInfo.SessionId = serverInfo.sessionId;
        _serverInfo.ServerAddress = serverInfo.serverAddress;
        _serverInfo.Status = $"Connected to {serverInfo.region} (cached)";
    }

    private (string region, string serverId, string sessionId, string serverAddress)? ParseLatestServerInfo(string logFilePath)
    {
        DebugLogger.Log("ParseLatestServerInfo called");

        try
        {
            // Read lines from the log file (limit for performance)
            var maxLines = _configuration.GetValue<int>("LogSettings:MaxLogLinesToRead", 5000);
            DebugLogger.Log($"Reading up to {maxLines} lines from log file");

            // Read all lines first, then take the last N lines (most recent)
            // Handle file locking by Prospect game using FileShare mode
            string[] allLines;
            try
            {
                // Try to read with FileShare.ReadWrite to access file even when locked
                using (var stream = File.Open(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var lines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    allLines = lines.ToArray();
                }
                DebugLogger.Log($"Log file has {allLines.Length} total lines (accessed with FileShare)");
            }
            catch (IOException ex)
            {
                DebugLogger.Log($"File access failed: {ex.Message}, will retry on next update");
                _serverInfo.DebugInfo = $"File access failed, retrying...";
                return null; // Return null so we don't show "no data found"
            }

            var startIndex = System.Math.Max(0, allLines.Length - maxLines);
            var recentLines = allLines.Skip(startIndex).Reverse().ToArray(); // Reverse to check newest first
            DebugLogger.Log($"Processing lines {allLines.Length - 1} to {startIndex} (newest first, {recentLines.Length} lines)");

            _serverInfo.DebugInfo = $"Reading {recentLines.Length} lines from log file (total: {allLines.Length}, max: {maxLines})";

            // Look for the latest TravelToServer line (skip localhost connections)
            int travelToServerCount = 0;
            foreach (string line in recentLines)
            {
                if (line.Contains("UYControllerTravelComponent::TravelToServer"))
                {
                    travelToServerCount++;
                    DebugLogger.Log($"Found TravelToServer line #{travelToServerCount} in log");

                    // Skip localhost connections (testing/development servers)
                    if (line.Contains("addr [127.0.0.1]"))
                    {
                        DebugLogger.Log($"Skipping localhost connection #{travelToServerCount}");
                        continue;
                    }

                    _serverInfo.DebugInfo = $"Found server connection at {DateTime.Now:HH:mm:ss}";
                    // Show a snippet of the line for debugging
                    var lineSnippet = line.Length > 150 ? line.Substring(0, 150) + "..." : line;
                    _serverInfo.DebugInfo += $"\nLine: {lineSnippet}";
                    DebugLogger.Log($"Server connection line: {lineSnippet}");
                    return ParseServerLine(line);
                }
            }

            DebugLogger.Log($"No TravelToServer lines found in the last {maxLines} lines");
            _serverInfo.DebugInfo = $" No TravelToServer lines found in {recentLines.Length} lines (searched last {maxLines})";
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Exception in ParseLatestServerInfo", ex);
            System.Diagnostics.Debug.WriteLine($"Error parsing log file: {ex.Message}");
        }

        return null;
    }

    private (string region, string serverId, string sessionId, string serverAddress)? ParseServerLine(string line)
    {
        DebugLogger.Log("ParseServerLine called");

        try
        {
            // Regular expression to match the server connection data
            // Pattern: [FYMatchConnectionData | addr [IP:PORT] sessionId [SESSION] serverId [SERVER] region [REGION] connectSinglePlayer [0] m_isMatch [1]]
            // Try multiple regex patterns to handle different log formats
            var patterns = new[]
            {
                @"addr \[([^\]]+)\]\s+sessionId \[([^\]]+)\]\s+serverId \[([^\]]+)\]\s+region \[([^\]]+)\]",
                @"addr\s*\[([^\]]+)\]\s*sessionId\s*\[([^\]]+)\]\s*serverId\s*\[([^\]]+)\]\s*region\s*\[([^\]]+)\]",
                @"addr\[([^\]]+)\] sessionId\[([^\]]+)\] serverId\[([^\]]+)\] region\[([^\]]+)\]"
            };

            DebugLogger.Log($"Trying {patterns.Length} regex patterns on line");

            Match? match = null;
            int patternIndex = 0;
            foreach (var pattern in patterns)
            {
                patternIndex++;
                DebugLogger.Log($"Trying pattern {patternIndex}: {pattern}");
                match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    DebugLogger.Log($"Pattern {patternIndex} matched!");
                    break;
                }
            }

            if (match != null && match.Success)
            {
                string serverAddress = match.Groups[1].Value;
                string sessionId = match.Groups[2].Value;
                string serverId = match.Groups[3].Value;
                string region = match.Groups[4].Value;

                DebugLogger.Log($"Successfully parsed: Region={region}, Server={serverAddress}, Session={sessionId}, ServerId={serverId}");
                _serverInfo.DebugInfo = $" SUCCESS: Region={region}, Server={serverAddress}";
                return (region, serverId, sessionId, serverAddress);
            }
            else
            {
                DebugLogger.Log("All regex patterns failed");
                // Show what we were trying to match
                var lineSnippet = line.Length > 200 ? line.Substring(0, 200) + "..." : line;
                _serverInfo.DebugInfo = $"REGEX FAILED on all patterns\nLine: {lineSnippet}";
                DebugLogger.Log($"Failed line: {lineSnippet}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogError("Exception in ParseServerLine", ex);
            System.Diagnostics.Debug.WriteLine($"Error parsing server line: {ex.Message}");
        }

        return null;
    }


    private void ShowSettingsDialog()
    {
        if (_settingsWindow != null)
        {
            // Window already exists, just focus it
            _settingsWindow.Focus();
            return;
        }

        var settingsWindow = new Window
        {
            Title = "Prospect Server Overlay - Settings",
            Width = 450,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true // Keep settings window on top like main overlay
        };

        _settingsWindow = settingsWindow; // Track the window

        var grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header with close button
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Log path
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Position label
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Position combo
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Debug label
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Debug checkbox
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hotkey label
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hotkey input
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Made by note
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Fortune quote

        // Header with close button
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var closeButton = new Button
        {
            Content = "✕",
            Width = 25,
            Height = 25,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        headerPanel.Children.Add(closeButton);

        // Log Path
        var logPathLabel = new TextBlock { Text = "Log File Path:", Margin = new Thickness(0, 0, 0, 5) };
        var logPathTextBox = new TextBox
        {
            Text = _configuration.GetValue<string>("LogSettings:LogFilePath", ""),
            Margin = new Thickness(0, 0, 0, 10),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Gray
        };

        // Position
        var positionLabel = new TextBlock { Text = "Screen Position:", Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(positionLabel, 2);

        // Create a 2x2 grid of buttons to visually represent screen positions
        var positionGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        positionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        positionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        positionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        positionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

        // Create buttons for each position
        var topLeftButton = new Button
        {
            Content = "↖",
            Width = 50,
            Height = 30,
            Background = Brushes.Black,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            FontSize = 16,
            FontWeight = FontWeights.Bold
        };

        var topRightButton = new Button
        {
            Content = "↗",
            Width = 50,
            Height = 30,
            Background = Brushes.Black,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            FontSize = 16,
            FontWeight = FontWeights.Bold
        };

        var bottomLeftButton = new Button
        {
            Content = "↙",
            Width = 50,
            Height = 30,
            Background = Brushes.Black,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            FontSize = 16,
            FontWeight = FontWeights.Bold
        };

        var bottomRightButton = new Button
        {
            Content = "↘",
            Width = 50,
            Height = 30,
            Background = Brushes.Black,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            FontSize = 16,
            FontWeight = FontWeights.Bold
        };

        // Get current position and highlight the selected button
        var currentLeft = Left;
        var currentTop = Top;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        string currentPosition = "Top-Left"; // default
        if (currentLeft > screenWidth / 2 && currentTop < screenHeight / 2)
            currentPosition = "Top-Right";
        else if (currentLeft < screenWidth / 2 && currentTop < screenHeight / 2)
            currentPosition = "Top-Left";
        else if (currentLeft < screenWidth / 2 && currentTop > screenHeight / 2)
            currentPosition = "Bottom-Left";
        else
            currentPosition = "Bottom-Right";

        // Highlight current position
        switch (currentPosition)
        {
            case "Top-Left":
                topLeftButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
                break;
            case "Top-Right":
                topRightButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
                break;
            case "Bottom-Left":
                bottomLeftButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
                break;
            case "Bottom-Right":
                bottomRightButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
                break;
        }

        // Position buttons in grid
        Grid.SetRow(topLeftButton, 0);
        Grid.SetColumn(topLeftButton, 0);
        Grid.SetRow(topRightButton, 0);
        Grid.SetColumn(topRightButton, 1);
        Grid.SetRow(bottomLeftButton, 1);
        Grid.SetColumn(bottomLeftButton, 0);
        Grid.SetRow(bottomRightButton, 1);
        Grid.SetColumn(bottomRightButton, 1);

        // Variable to track selected position
        string selectedPosition = currentPosition;

        // Add click handlers to update selected position and button highlights
        topLeftButton.Click += (s, e) =>
        {
            selectedPosition = "Top-Left";
            // Reset all buttons to black
            topLeftButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
            topRightButton.Background = Brushes.Black;
            bottomLeftButton.Background = Brushes.Black;
            bottomRightButton.Background = Brushes.Black;
        };

        topRightButton.Click += (s, e) =>
        {
            selectedPosition = "Top-Right";
            // Reset all buttons to black
            topLeftButton.Background = Brushes.Black;
            topRightButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
            bottomLeftButton.Background = Brushes.Black;
            bottomRightButton.Background = Brushes.Black;
        };

        bottomLeftButton.Click += (s, e) =>
        {
            selectedPosition = "Bottom-Left";
            // Reset all buttons to black
            topLeftButton.Background = Brushes.Black;
            topRightButton.Background = Brushes.Black;
            bottomLeftButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
            bottomRightButton.Background = Brushes.Black;
        };

        bottomRightButton.Click += (s, e) =>
        {
            selectedPosition = "Bottom-Right";
            // Reset all buttons to black
            topLeftButton.Background = Brushes.Black;
            topRightButton.Background = Brushes.Black;
            bottomLeftButton.Background = Brushes.Black;
            bottomRightButton.Background = new SolidColorBrush(Color.FromRgb(0, 120, 0));
        };

        positionGrid.Children.Add(topLeftButton);
        positionGrid.Children.Add(topRightButton);
        positionGrid.Children.Add(bottomLeftButton);
        positionGrid.Children.Add(bottomRightButton);

        Grid.SetRow(positionGrid, 3);

        // Debug toggle
        var debugLabel = new TextBlock { Text = "Debug Info:", Margin = new Thickness(0, 0, 0, 5) };
        Grid.SetRow(debugLabel, 4);

        var debugCheckBox = new CheckBox
        {
            Content = "Show debug information",
            IsChecked = _serverInfo.DebugVisible,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Brushes.White
        };
        Grid.SetRow(debugCheckBox, 5);

        // Hotkey settings
        var hotkeyLabel = new TextBlock { Text = "Settings Hotkey:", Margin = new Thickness(0, 0, 0, 5) };
        Grid.SetRow(hotkeyLabel, 6);

        var hotkeyTextBox = new TextBox
        {
            Text = _settingsHotkey,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Brushes.Black,
            Background = Brushes.White,
            Padding = new Thickness(5),
            IsReadOnly = true
        };
        Grid.SetRow(hotkeyTextBox, 7);

        // Make the textbox clickable to capture new hotkey
        hotkeyTextBox.PreviewMouseDown += (s, e) =>
        {
            hotkeyTextBox.Text = "Press new key combination...";
            hotkeyTextBox.Background = Brushes.Yellow;
            _isCapturingHotkey = true;
            hotkeyTextBox.Focus(); // Ensure textbox has focus
            e.Handled = true;
        };

        // Use KeyDown instead of PreviewKeyDown for more reliable capture
        hotkeyTextBox.KeyDown += (s, e) =>
        {
            DebugLogger.Log($"KeyDown: Key={e.Key}, Modifiers={System.Windows.Input.Keyboard.Modifiers}, IsCapturing={_isCapturingHotkey}");

            if (_isCapturingHotkey)
            {
                var newHotkey = CaptureHotkey(e);
                DebugLogger.Log($"CaptureHotkey returned: '{newHotkey}'");

                if (!string.IsNullOrEmpty(newHotkey))
                {
                    _settingsHotkey = newHotkey;
                    hotkeyTextBox.Text = newHotkey;
                    hotkeyTextBox.Background = Brushes.White;
                    _isCapturingHotkey = false;
                    DebugLogger.Log($"Hotkey changed to: {newHotkey}");

                    // Re-register the hotkey with the new combination
                    Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            WindowUtils.UnregisterGlobalHotkey();
                            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                            if (WindowUtils.RegisterGlobalHotkey(windowHandle, _settingsHotkey))
                            {
                                DebugLogger.Log($"Hotkey re-registered with new combination: {_settingsHotkey}");
                            }
                            else
                            {
                                DebugLogger.LogError($"Failed to re-register hotkey with: {_settingsHotkey}", null);
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError("Error re-registering hotkey", ex);
                        }
                    });
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    // Cancel hotkey capture
                    hotkeyTextBox.Text = _settingsHotkey;
                    hotkeyTextBox.Background = Brushes.White;
                    _isCapturingHotkey = false;
                    DebugLogger.Log("Hotkey capture cancelled with Escape");
                }
                else
                {
                    DebugLogger.Log("Invalid hotkey captured - ignoring");
                }
                e.Handled = true;
            }
        };

        // Developer credit
        var madeByLabel = new TextBlock
        {
            Text = "Made using a toaster without a toaster... Ondario 2025",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 15)
        };
        Grid.SetRow(madeByLabel, 8);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        Grid.SetRow(buttonPanel, 9);

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80,
            Height = 30,
            Margin = new Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 0)),
            Foreground = Brushes.White
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 30
        };

        var closeAppButton = new Button
        {
            Content = "Close App",
            Width = 80,
            Height = 30,
            Margin = new Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush(Color.FromRgb(120, 0, 0)),
            Foreground = Brushes.White
        };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(closeAppButton);
        buttonPanel.Children.Add(cancelButton);

        // Add elements to grid
        grid.Children.Add(headerPanel); // Header with close button

        grid.Children.Add(logPathLabel);
        Grid.SetRow(logPathLabel, 1);
        grid.Children.Add(logPathTextBox);
        Grid.SetRow(logPathTextBox, 1);

        grid.Children.Add(positionLabel);
        grid.Children.Add(positionGrid);
        grid.Children.Add(debugLabel);
        grid.Children.Add(debugCheckBox);
        grid.Children.Add(hotkeyLabel);
        grid.Children.Add(hotkeyTextBox);
        grid.Children.Add(madeByLabel);
        grid.Children.Add(buttonPanel);

        // Fortune Favors the Bold quote
        var fortuneQuote = new TextBlock
        {
            Text = "Fortune Favors the Bold",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 10)
        };
        Grid.SetRow(fortuneQuote, 10);
        grid.Children.Add(fortuneQuote);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)), // #CC000000
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)), // #00FF00
            BorderThickness = new Thickness(1),
            Margin = new Thickness(10),
            Padding = new Thickness(10),
            Child = grid
        };

        settingsWindow.Content = border;

        // Event handlers
        closeButton.Click += (s, e) => settingsWindow.Close();

        saveButton.Click += (s, e) =>
        {
            try
            {
                // Collect all current settings
                var newLogPath = logPathTextBox.Text;
                var newPosition = selectedPosition;
                var debugEnabled = debugCheckBox.IsChecked ?? false;

                // Update in-memory settings
                _serverInfo.DebugVisible = debugEnabled;

                // Get current window position for saving
                var currentLeft = Left;
                var currentTop = Top;

                // Create settings object to serialize
                var settings = new
                {
                    OverlaySettings = new
                    {
                        WindowPosition = new
                        {
                            Left = currentLeft,
                            Top = currentTop
                        },
                        Opacity = Opacity,
                        UpdateIntervalSeconds = 5, // Could make this configurable later
                        ShowServerId = false, // Could make this configurable later
                        AutoStartMinimized = false, // Could make this configurable later 
                        DebugVisible = debugEnabled,
                        SettingsHotkey = _settingsHotkey
                    },
                    LogSettings = new
                    {
                        LogFilePath = string.IsNullOrEmpty(newLogPath) ? "%LOCALAPPDATA%\\Prospect\\Saved\\Logs\\Prospect.log" : newLogPath,
                        MaxLogLinesToRead = 5000
                    }
                };

                // Serialize to JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var jsonString = JsonSerializer.Serialize(settings, options);

                // Write to config file
                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                File.WriteAllText(configPath, jsonString);

                // Update window position
                UpdateWindowPosition(newPosition);

                DebugLogger.Log($"Settings saved to config file:");
                DebugLogger.Log($"  DebugVisible: {debugEnabled}");
                DebugLogger.Log($"  SettingsHotkey: {_settingsHotkey}");
                DebugLogger.Log($"  WindowPosition: ({currentLeft}, {currentTop})");
                DebugLogger.Log($"  LogPath: {newLogPath}");

                        // Ensure main window stays on top immediately after settings change
                Dispatcher.Invoke(() => ForceTopMostImmediately());

                settingsWindow.Close();
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("Error saving settings to config file", ex);
                // Still close the dialog even if saving failed
                settingsWindow.Close();
            }
        };

        cancelButton.Click += (s, e) => settingsWindow.Close();

        bool shouldCloseApp = false;

        closeAppButton.Click += (s, e) =>
        {
            shouldCloseApp = true;
            settingsWindow.Close();
        };

        settingsWindow.Closed += (s, e) =>
        {
            _settingsWindow = null; // Clear the reference
            if (shouldCloseApp)
            {
                Close(); // Close the main application
            }
        };

        // Handle hotkey capture at window level for reliability
        settingsWindow.PreviewKeyDown += (s, e) =>
        {
            DebugLogger.Log($"SettingsWindow PreviewKeyDown: Key={e.Key}, IsCapturing={_isCapturingHotkey}");

            if (_isCapturingHotkey)
            {
                var newHotkey = CaptureHotkey(e);
                DebugLogger.Log($"Window-level CaptureHotkey returned: '{newHotkey}'");

                if (!string.IsNullOrEmpty(newHotkey))
                {
                    _settingsHotkey = newHotkey;
                    hotkeyTextBox.Text = newHotkey;
                    hotkeyTextBox.Background = Brushes.White;
                    _isCapturingHotkey = false;
                    DebugLogger.Log($"Hotkey changed to: {newHotkey}");

                    // Re-register the hotkey with the new combination
                    Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            WindowUtils.UnregisterGlobalHotkey();
                            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                            if (WindowUtils.RegisterGlobalHotkey(windowHandle, _settingsHotkey))
                            {
                                DebugLogger.Log($"Hotkey re-registered with new combination: {_settingsHotkey}");
                            }
                            else
                            {
                                DebugLogger.LogError($"Failed to re-register hotkey with: {_settingsHotkey}", null);
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.LogError("Error re-registering hotkey", ex);
                        }
                    });
                }
                else if (e.Key == System.Windows.Input.Key.Escape)
                {
                    // Cancel hotkey capture
                    hotkeyTextBox.Text = _settingsHotkey;
                    hotkeyTextBox.Background = Brushes.White;
                    _isCapturingHotkey = false;
                    DebugLogger.Log("Hotkey capture cancelled with Escape");
                }
                else
                {
                    DebugLogger.Log("Invalid hotkey captured - ignoring");
                }
                e.Handled = true;
            }
        };

        // Ensure settings window stays on top of fullscreen applications
        settingsWindow.Activated += (s, e) =>
        {
            try
            {
                var settingsHandle = new System.Windows.Interop.WindowInteropHelper(settingsWindow).Handle;
                WindowUtils.ForceTopMost(settingsHandle);
            }
            catch (Exception ex)
            {
                DebugLogger.LogError("Error forcing settings window to top", ex);
            }
        };

        // Focus the window when it opens
        settingsWindow.Loaded += (s, e) =>
        {
            settingsWindow.Focus();
            settingsWindow.Activate();
        };

        settingsWindow.ShowDialog();
    }

    private void UpdateWindowPosition(string? position)
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        var windowWidth = Width;
        var windowHeight = Height;

        switch (position)
        {
            case "Top-Left":
                Left = 20;
                Top = 20;
                break;
            case "Top-Right":
                Left = screenWidth - windowWidth - 20;
                Top = 20;
                break;
            case "Bottom-Left":
                Left = 20;
                Top = screenHeight - windowHeight - 20;
                break;
            case "Bottom-Right":
                Left = screenWidth - windowWidth - 20;
                Top = screenHeight - windowHeight - 20;
                break;
        }

        // Ensure window stays on top after position change
        Dispatcher.Invoke(() => ForceTopMostImmediately());
    }

    protected override void OnClosed(EventArgs e)
    {
        DebugLogger.Log("Application closing - stopping timers, unregistering hotkey, and disposing file watcher");
        _updateTimer.Stop();
        _topmostTimer.Stop();

        // Unregister the global hotkey
        WindowUtils.UnregisterGlobalHotkey();
        DebugLogger.Log("Global hotkey unregistered");

        // Remove message filter
        ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcher_ThreadFilterMessage;
        DebugLogger.Log("ComponentDispatcher message filter removed");

        _fileWatcher?.Dispose();
        DebugLogger.Log("Application shutdown complete");
        base.OnClosed(e);
    }
}