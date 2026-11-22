# Logitech T650 Tray App for Windows

A Windows tray app that brings multi-touch gestures to the **Logitech Wireless Rechargeable Touchpad T650** over the Logitech Unifying Receiver (`VID 0x046D`, `PID 0xC52B`).

This is a fork of [brunnobb/Logitech-T650-Windows-PTP](https://github.com/brunnobb/Logitech-T650-Windows-PTP) with the virtual PTP driver removed. It is a normal user-mode program: no driver, no test-signing, no certificates, and no changes to boot settings.

---

## Background

The Logitech T650 was released during the Windows 7 / 8 era. On Windows 10 and 11, Logitech's legacy software is discontinued and only maps gestures to old keyboard shortcuts. This app unlocks the T650's raw multi-touch stream and turns it into pointer movement, clicks, scrolling and gesture shortcuts.

---

## How It Works

```text
┌────────────────────────────────────────────────────────┐
│  T650Bridge tray app (C# .NET 10)                      │
│  - Reads raw reports from the Logitech Unifying        │
│    Receiver via HidSharp                               │
│  - Handshakes Logitech HID++ 2.0 (TOUCHPAD_RAW_XY)     │
│  - Reassembles multi-contact frames & coordinates      │
│  - Gesture engine sends mouse and keyboard input       │
│    through the Windows user32 input APIs               │
└────────────────────────────────────────────────────────┘
```

### Features
- **Protocol unlock:** resolves Feature `0x6100` (`TOUCHPAD_RAW_XY`) at runtime and sends `setRawReportState(0x05)` to start raw multi-touch streaming.
- **Heartbeat:** keeps the 2.4 GHz Unifying connection alive.
- **Hotplug:** detects the receiver being plugged in and the pad powering on or off, and re-unlocks raw mode automatically.
- **Follows your Windows touchpad settings:** the app reads `Settings > Bluetooth & devices > Touchpad` (stored in `HKCU\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad`) and re-reads it every 2 seconds, so changes apply without a restart. Missing values use the Windows 11 defaults.

  | Windows setting | Registry value | Used for |
  |---|---|---|
  | Taps | `TapsEnabled` | 1-finger and 2-finger taps |
  | Tap with two fingers to right-click | `TwoFingerTapEnabled` | 2-finger tap |
  | Tap twice and drag to multi-select | `TapAndDrag` | Tap-and-drag |
  | Press the lower right corner to right-click | `RightClickZoneEnabled` | Physical click in bottom-right corner |
  | Drag two fingers to scroll | `PanEnabled` | 2-finger scroll |
  | Pinch to zoom | `ZoomEnabled` | Pinch zoom |
  | Scrolling direction | `ScrollDirection` | Vertical scroll direction (`0` = natural, content follows fingers) |
  | Cursor speed | `CursorSpeed` | Pointer speed (`10` = default) |
  | Three-finger swipes / tap | `ThreeFingerSlideEnabled` / `ThreeFingerTapEnabled` | 3-finger gestures |
  | Four-finger swipes / tap | `FourFingerSlideEnabled` / `FourFingerTapEnabled` | 4-finger gestures |

  The meanings of the 3/4-finger and `ScrollDirection` values come from community documentation, not an official Microsoft table. Swipes: `0` switch apps, `1` switch desktops, `2` audio and volume. Taps: `0` search, `1` notification center, `2` play/pause, `3` middle click. Other values do nothing.
- **Gestures:**
  - 1-finger glide: pointer motion with acceleration, scaled by cursor speed.
  - 1-finger tap: left click.
  - Tap-and-drag: tap, then touch again within 300 ms and move to drag. Lifting ends the drag.
  - Physical button click: left button down/up, with drag and drop. A press with two fingers, or with one finger in the bottom-right corner, is a right click.
  - 2-finger scroll: vertical and horizontal scrolling.
  - 2-finger pinch: zoom in / out (`Ctrl + wheel`).
  - 2-finger tap: right click.
  - 3/4-finger swipes, depending on the Windows setting:
    - **Switch apps and show desktop:** left / right opens the app switcher and keeps it open while your fingers stay down; keep moving to step through apps, lift to pick. Up: Task View (`Win + Tab`). Down: Show Desktop (`Win + D`).
    - **Switch desktops and show desktop:** left / right goes to the next / previous virtual desktop (`Ctrl + Win + Right` / `Ctrl + Win + Left`). Up: Task View. Down: Show Desktop.
    - **Change audio and volume:** up / down changes volume, and keeps stepping while you move. Right / left: next / previous track.
  - 3/4-finger taps, depending on the Windows setting: Search (`Win + S`), Notification Center (`Win + N`), Play/Pause or middle click.
  - Defaults: 3-finger swipes switch apps, 3-finger tap opens Search, 4-finger swipes switch desktops, 4-finger tap opens Notification Center.
- **Tray icon:** status, pause/resume gestures, re-wake the pad, toggle notifications, toggle run at login, show/hide the console, exit.
- **Run at login:** the tray menu's **Run at Login** item adds or removes a per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry that starts the running exe with `--silent`.

### Limitations
- Windows does not see the T650 as a Precision Touchpad. The touchpad settings page only shows up if this PC also has a real precision touchpad; otherwise the defaults above are used.
- Scrolling and zoom use mouse wheel events, not native touchpad scrolling. Zoom only works in apps that zoom on `Ctrl + wheel`.
- Desktop switching jumps one desktop per swipe, without the animation that follows your fingers.
- Edge swipes, touchpad sensitivity (`AAPThreshold`) and "leave touchpad on when a mouse is connected" are not supported.
- Gestures do not reach elevated (Administrator) windows because of Windows UIPI. Use **Restart as Administrator** in the tray menu if you need that.
- Gestures only work while the app is running.

---

## Command-Line Options

| Option | Effect |
|---|---|
| `--silent`, `--tray`, `--minimized` | Start hidden in the tray |
| `--console`, `--no-hide` | Keep the console window visible |
| `--diag`, `--test` | Show live per-frame contact diagnostics |
| `--no-gesture` | Start with gestures paused |
| `--notifications`, `--balloon` | Enable desktop notifications |
| `--test-only` | Print startup checks and exit |

Console keys: `r` re-wakes the pad, `h` hides/shows the console, `q` exits.

---

## Project Structure

```text
├── T650-Windows-PTP-Driver/
│   └── T650Bridge/               # .NET C# tray app
│       ├── Hidpp/                # Logitech Unifying HID++ 2.0 communication
│       ├── Touch/                # Frame reassembler & coordinate parsing
│       ├── Gesture/              # Gesture engine
│       ├── UI/                   # Tray icon & icon generation
│       └── Program.cs            # Entry point
├── Installer/                    # WiX 3.14 MSI installer & scripts
│   ├── T650Bridge/               # Tray app MSI definition & build script
│   ├── Build-All-Installers.ps1  # Builds the MSI and SHA256SUMS.txt
│   └── Bump-Version.ps1          # Version bump and rebuild script
└── Dist/                         # Built MSI and checksums (not committed)
```

---

## How to Build

### Prerequisites
* Windows 10 (1903+) or Windows 11 (x64)
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [WiX Toolset v3.14](https://wixtoolset.org/) (only for building the MSI)
* PowerShell 7 (`pwsh`) (only for the installer scripts)

### Build and run the app
```powershell
dotnet build "T650-Windows-PTP-Driver\T650Bridge" -c Release
dotnet run --project "T650-Windows-PTP-Driver\T650Bridge" -c Release -- --console
```

### Publish
```powershell
dotnet publish "T650-Windows-PTP-Driver\T650Bridge" -c Release -r win-x64 --no-self-contained -o "Installer\T650Bridge\bin\publish"
```

### Build the MSI installer
```powershell
pwsh -File "Installer\Build-All-Installers.ps1" -Version "1.1.6"
```
Output in `Dist\`:
* `Dist\T650Bridge-Setup-v1.1.6.msi`
* `Dist\SHA256SUMS.txt`

The MSI installs to Program Files, adds Start menu and desktop shortcuts, and adds a per-user autostart entry (`HKCU\...\Run`) that you can turn off in Task Manager.

### Bump the version
```powershell
pwsh -File "Installer\Bump-Version.ps1" -NewVersion "1.1.7"
```

---

## Attributions & References

1. **[brunnobb/Logitech-T650-Windows-PTP](https://github.com/brunnobb/Logitech-T650-Windows-PTP)**: the original project this fork is based on.
2. **`logitec-t650-macos-driver`**: protocol specification, bit-packing definitions and touch session models.
3. **`T650drivers`**: early Windows experiments and device discovery over the Unifying receiver.
4. **Linux kernel (`hid-logitech-hidpp.c`)**: reference for the Logitech HID++ 2.0 protocol and Feature `0x6100` (`TOUCHPAD_RAW_XY`).

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
