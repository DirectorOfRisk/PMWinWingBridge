# First Project Magenta serial capture

## Preconditions

- Windows 11 x64
- Virtual null-modem pair `COM10 ↔ COM11`
- Project Magenta CDU build 545
- `cdu.ini`: `EngravityComm=10`

## Run order

1. Close Project Magenta CDU.exe.
2. Start `START_SERIAL_MONITOR.bat`.
3. Confirm that the monitor reports `COM11 ist geöffnet`.
4. Start Project Magenta CDU.exe.
5. Change CDU pages and type a few characters in the Project Magenta CDU window.
6. Leave the monitor running for 30–60 seconds.
7. Press Enter in the serial monitor.
8. Retrieve the newest file from the tool's `logs` directory.

Do not post logs publicly until they have been checked for personal paths or other sensitive data.
