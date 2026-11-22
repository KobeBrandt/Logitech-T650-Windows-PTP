using System.Reflection;
using System.Security.Principal;
using T650Bridge.Gesture;
using T650Bridge.Hidpp;
using T650Bridge.Interop;
using T650Bridge.Touch;
using T650Bridge.UI;

namespace T650Bridge;

internal static class Program
{
    private static readonly TimeSpan SettingsRefreshInterval = TimeSpan.FromSeconds(2);

    static void Main(string[] args)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

        bool isElevated = IsElevated();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("===============================================================");
        Console.WriteLine($" Logitech T650 Tray v{version} ");
        Console.WriteLine("===============================================================");
        Console.ResetColor();

        Console.WriteLine($"[Version] Logitech T650 Tray v{version}");
        string securityStatus = isElevated
            ? "Administrator (UIPI Bypass Active)"
            : "Standard User (Medium Integrity)";

        Console.WriteLine($"[Security] Elevation: {securityStatus}");
        if (!isElevated)
        {
            Console.WriteLine("[Security] Notice: Gestures do not reach Admin windows. Use 'Restart as Administrator' in the tray menu if needed.");
        }
        bool enableGestures = !args.Contains("--no-gesture");
        bool enableNotifications = args.Contains("--notifications") || args.Contains("--balloon");
        bool verboseDiag = args.Contains("--diag") || args.Contains("--test") || (!args.Contains("--silent") && !args.Contains("--tray"));

        Console.WriteLine($"[Config] Gestures Enabled: {enableGestures}");
        Console.WriteLine($"[Config] Desktop Notifications: {(enableNotifications ? "Enabled" : "Disabled (Quiet)")}");
        const string MutexName = @"Global\LogitechT650Bridge_SingleInstanceMutex";
        using var singleMutex = new Mutex(true, MutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[Notice] Another instance of Logitech T650 Tray is already running. Exiting.");
            Console.ResetColor();
            return;
        }

        InteractiveDesktop.Attach();

        bool hasDesktop = InteractiveDesktop.TryGetCursorPosition(out int cursorX, out int cursorY);
        Console.WriteLine($"[Desktop] Interactive winsta0 access: {(hasDesktop ? $"OK (Cursor at {cursorX},{cursorY})" : "Limited")}");

        if (args.Contains("--test-only")) return;

        Console.WriteLine("Press Ctrl+C to terminate.\n");

        using var receiver = new UnifyingReceiver();
        var assembler = new FrameAssembler();
        var gestureEngine = new PrecisionGestureEngine();

        receiver.LogMessage += msg => Console.WriteLine($"[Receiver] {msg}");
        receiver.ReportReceived += report => assembler.ProcessRawReport(report);

        gestureEngine.GestureTriggered += gesture =>
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n>>> {gesture} <<<");
            Console.ResetColor();
        };

        long frameIndex = 0;

        assembler.FrameReady += frame =>
        {
            InteractiveDesktop.EnsureAttachedForCurrentThread();
            frameIndex++;

            if (enableGestures)
            {
                gestureEngine.ProcessFrame(frame);
            }

            // Diagnostic console output
            if (verboseDiag)
            {
                var touching = frame.Contacts.Where(c => c.IsTouching).ToList();
                string contactsInfo = touching.Count == 0 
                    ? "None (Lifted)" 
                    : string.Join(", ", touching.Select(c => $"F{c.Id}:({c.X,4},{c.Y,4})"));

                string clickInfo = frame.PhysicalButtonPressed ? "[CLICK]" : "       ";

                Console.Write($"\r[Frame #{frameIndex:D5}] Active: {touching.Count}/5 {clickInfo} | Contacts: {contactsInfo,-45}");
            }
        };

        bool silentStartup = args.Contains("--silent") || args.Contains("--tray") || args.Contains("--minimized");
        bool autoHide = !args.Contains("--console") && !args.Contains("--no-hide");

        gestureEngine.Settings = TouchpadSettings.Load();
        Console.WriteLine($"[Config] Windows touchpad settings: {gestureEngine.Settings}");

        // Re-read Windows touchpad settings so changes in Settings > Touchpad apply without a restart.
        using var settingsTimer = new System.Threading.Timer(_ =>
        {
            var latest = TouchpadSettings.Load();
            if (latest == gestureEngine.Settings) return;
            gestureEngine.Settings = latest;
            Console.WriteLine($"\n[Config] Windows touchpad settings changed: {latest}");
        }, null, SettingsRefreshInterval, SettingsRefreshInterval);

        using var tray = new TrayIconManager(isElevated);
        tray.EnableNotifications = enableNotifications;
        tray.UpdateState(DeviceConnectionState.SearchingForReceiver, isPaused: !enableGestures);

        if (silentStartup)
        {
            tray.HideConsole(notifyUser: false);
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        tray.ToggleStartStop += () =>
        {
            enableGestures = !enableGestures;
            string stateMsg = enableGestures ? "Active (Gestures Running)" : "Paused (Standard Mouse Only)";
            Console.WriteLine($"\n[Tray] Gestures toggled -> {stateMsg}");
            tray.UpdateState(receiver.State, isPaused: !enableGestures);
        };

        tray.RequestRewake += () =>
        {
            Console.WriteLine("\n[Tray] Re-sending raw multi-touch unlock packet to T650...");
            receiver.UnlockRawMode();
            tray.ShowBalloon("Logitech T650", "Re-sent raw touch mode unlock command to pad.");
        };

        tray.RequestExit += () =>
        {
            Console.WriteLine("\n[Tray] Exit requested from taskbar menu.");
            cts.Cancel();
        };

        bool hasEverStreamed = false;

        receiver.StateChanged += state =>
        {
            tray.UpdateState(state, isPaused: !enableGestures);

            switch (state)
            {
                case DeviceConnectionState.SearchingForReceiver:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[Receiver] Waiting for Logitech Unifying receiver to be plugged in...");
                    Console.ResetColor();
                    tray.ShowBalloon("Logitech T650", "Waiting for Logitech Unifying receiver USB dongle.", System.Windows.Forms.ToolTipIcon.Warning);
                    break;

                case DeviceConnectionState.ReceiverConnectedWaitingForPad:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("\n[Receiver] Unifying receiver ready! Waiting for T650 Touchpad to power on...");
                    Console.ResetColor();
                    tray.ShowBalloon("Logitech T650", "Unifying receiver connected. Turn on your T650 touchpad.", System.Windows.Forms.ToolTipIcon.Info);
                    break;

                case DeviceConnectionState.TouchpadActive:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n[Active] T650 Multi-touch stream is live! Try touching, scrolling, or swiping on the pad.\n");
                    Console.ResetColor();

                    if (!hasEverStreamed)
                    {
                        hasEverStreamed = true;
                        if (autoHide && !silentStartup && !tray.IsConsoleHidden)
                        {
                            Console.WriteLine("[Tray] Pad connected and active! Minimizing console to system tray...");
                            Thread.Sleep(600);
                            tray.HideConsole(notifyUser: true);
                        }
                        else
                        {
                            tray.ShowBalloon("Logitech T650 Active", "Multi-touch gestures ready. Right-click taskbar icon to manage.", System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                    break;
            }
        };

        receiver.StartSupervisor();

        Console.WriteLine("[Tray] Taskbar notification icon active. Right-click icon for start/stop & controls.");
        Console.WriteLine("[Keys] Press 'r' anytime to re-wake/unlock | Press 'h' to hide/show console | Press 'q' to exit.\n");

        bool canReadKey = false;
        try
        {
            canReadKey = !Console.IsInputRedirected;
        }
        catch
        {
            canReadKey = false;
        }

        while (!cts.IsCancellationRequested)
        {
            if (canReadKey)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.R)
                        {
                            Console.WriteLine("\n[User] Re-sending raw multi-touch unlock packet to T650...");
                            receiver.UnlockRawMode();
                        }
                        else if (key.Key == ConsoleKey.H)
                        {
                            tray.ToggleConsoleWindow();
                        }
                        else if (key.Key == ConsoleKey.Q)
                        {
                            cts.Cancel();
                            break;
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    canReadKey = false;
                }
            }
            Thread.Sleep(50);
        }

        Console.WriteLine("\nShutting down bridge daemon...");
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
