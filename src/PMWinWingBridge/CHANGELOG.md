# Changelog

All notable changes to PMWinWingBridge are documented here.

## 1.0.0 — 2026-08-07

First stable release.

### Project Magenta integration
- Bidirectional Project Magenta CDU serial protocol support.
- Confirmed Project Magenta CDU key mapping.
- Full alphanumeric keypad support.
- Decimal point and positive/negative key support.
- Captain/F/O line-select keys.
- Function keys including INIT REF, RTE, CLB, CRZ, DES, MENU, LEGS,
  DEP ARR, HOLD, PROG, EXEC, N1 LIMIT, FIX, PREV PAGE and NEXT PAGE.
- CDU display mirroring to WinWing PFP-3N.
- Project Magenta LED state mirroring.

### WinWing integration
- WinWing PFP-3N USB/HID support via mcdu-dotnet.
- Automatic bitmap-font upload.
- Font upload repeated after USB reconnect.
- Automatic USB reconnect.
- Display, backlight and LED brightness configuration.

### Stability
- Automatic COM reconnect.
- Project Magenta CDU.exe / RCDU.exe can be restarted while the bridge runs.
- Persistent configuration.
- Logging.
- Clean shutdown.

### Multi-CDU
- Captain, First Officer and Observer roles.
- Separate Project Magenta COM link per role.
- Persistent physical-device identity.
- Device setup wizard.
- Existing assignments can be retained by pressing ENTER.
- COM input accepts `4`, `com4`, `Com4` or `COM4` and stores `COM4`.

### Packaging
- BSD 3-Clause License.
- Third-party notices.
- English README.
- Release build script.
