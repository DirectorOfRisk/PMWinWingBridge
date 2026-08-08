# PMWinWingBridge

**PMWinWingBridge** is an open-source bridge between Project Magenta CDU
software and supported WinWing CDU hardware, developed primarily for the
WinWing PFP-3N.

Copyright © 2026 Robert Kossakowski  
Licensed under the BSD 3-Clause License.  
Developed with assistance from OpenAI ChatGPT.

## Features

- Project Magenta CDU display mirrored to WinWing PFP-3N
- Full CDU keyboard input
- Left and right LSK support
- EXEC / MSG / FAIL / DSPY / OFST LED synchronization
- Decimal point and `+/-` support
- Automatic USB reconnect
- Automatic Project Magenta / COM reconnect
- CDU bitmap-font upload after every USB reconnect
- Persistent configuration
- Multi-CDU operation
- Captain, First Officer and Observer roles
- Independent COM link for every configured CDU
- Persistent physical WinWing device assignment

## Requirements

- Windows
- .NET 8 runtime for the framework-dependent release build
- Project Magenta CDU software
- WinWing PFP-3N CDU hardware
- One virtual null-modem COM pair per Project Magenta CDU instance

For development/building from source, install the .NET 8 SDK.

## Project Magenta serial connection

Project Magenta's Engravity CDU interface communicates over a Windows serial
port. PMWinWingBridge therefore uses the opposite end of a virtual null-modem
pair.

Example for Captain:

```text
Project Magenta CDU.exe    COM3
             ↕
     virtual null-modem
             ↕
PMWinWingBridge            COM4
             ↕
       WinWing USB/HID
             ↕
         PFP-3N
```

Example with Captain and First Officer:

```text
Captain:
  Project Magenta CDU.exe   -> COM3
  PMWinWingBridge           -> COM4

First Officer:
  Project Magenta RCDU.exe  -> COM5
  PMWinWingBridge           -> COM6
```

A third pair can be used for an Observer CDU.

## Installing a virtual COM-port pair

PMWinWingBridge does not require a specific virtual-serial product. It only
needs a reliable Windows null-modem pair where bytes written to one COM port
are received on the other COM port.

### Option 1 — HHD Virtual Serial Ports

HHD Virtual Serial Ports is a current commercial/shareware solution with
Windows 10/11 support.

Typical setup:

1. Install HHD Virtual Serial Ports.
2. Create a virtual null-modem pair, for example `COM3 <-> COM4`.
3. Configure Project Magenta CDU.exe to use COM3.
4. Configure the Captain profile in PMWinWingBridge to use COM4.
5. For a second CDU create another pair, for example `COM5 <-> COM6`.

HHD is not required specifically; it is simply one compatible solution.

### Option 2 — com0com (free / open source)

com0com is a free/open-source null-modem emulator for Windows and can create
virtual COM-port pairs.

A typical pair is configured so that one endpoint is exposed to Project
Magenta and the other endpoint to PMWinWingBridge.

**Important:** the long-standing SourceForge com0com builds are old. Their
own documentation notes Windows driver-signing limitations on Windows 10
1607 and later, and some older installation paths may conflict with Secure
Boot. Do not disable Secure Boot solely for PMWinWingBridge unless you
understand and accept the security implications. Use a properly signed,
trusted build that is compatible with your Windows installation, or choose a
current commercial virtual-serial solution instead.

PMWinWingBridge is independent of the chosen null-modem software.

## Project Magenta configuration

For the tested Captain setup:

```text
Project Magenta CDU.exe: COM3
PMWinWingBridge Captain: COM4
Baud rate:               38400
Data bits:               8
Parity:                  None
Stop bits:               1
Flow control:            None
```

Project Magenta's CDU configuration must select the corresponding Engravity
COM port.

For F/O use a separate Project Magenta RCDU instance and a separate COM pair.

## First start / device setup

If no physical CDU assignment exists yet, PMWinWingBridge starts the device
setup wizard.

For every new WinWing PFP-3N it asks:

```text
1 = Captain
2 = First Officer
3 = Observer
0 = Skip
```

It then asks for the **bridge-side** COM port.

Example:

```text
Project Magenta CDU.exe  -> COM3
PMWinWingBridge          -> COM4
```

The setup accepts:

```text
4
COM4
com4
Com4
```

and stores the normalized value:

```text
COM4
```

If the device is already configured, the setup shows its current role and
COM port. Press **ENTER** to keep the existing value or type a new COM port.

You can run the setup again later with:

```text
SETUP_DEVICES.bat
```

## Changing configuration later

Persistent settings are stored in:

```text
bridge-config.json
```

For convenience:

```text
CONFIG_BEARBEITEN.bat
```

opens the file in Notepad.

Do not edit the configuration while the bridge is writing settings.

## Physical CDU assignment

Every configured CDU has a stored `UsbIdentity`.

The bridge prefers a USB serial number when mcdu-dotnet exposes one:

```text
SERIAL:<device serial number>
```

This is the preferred identity because it remains attached to the same
physical device across reconnects and USB-port changes.

If a serial number is not exposed, the bridge can use a fallback HID/device
identity. The setup prints a warning in that case.

For normal multi-CDU operation the bridge does not intentionally select the
"first PFP-3N found"; it searches for the identity assigned to Captain,
First Officer or Observer.

## Example multi-CDU configuration

```json
{
  "Devices": [
    {
      "Role": "Captain",
      "UsbIdentity": "SERIAL:CAPTAIN_DEVICE_ID",
      "UsbDescription": "",
      "ComPort": "COM4",
      "BaudRate": 38400,
      "Enabled": true
    },
    {
      "Role": "FirstOfficer",
      "UsbIdentity": "SERIAL:FIRST_OFFICER_DEVICE_ID",
      "UsbDescription": "",
      "ComPort": "COM6",
      "BaudRate": 38400,
      "Enabled": true
    },
    {
      "Role": "Observer",
      "UsbIdentity": "SERIAL:OBSERVER_DEVICE_ID",
      "UsbDescription": "",
      "ComPort": "COM8",
      "BaudRate": 38400,
      "Enabled": false
    }
  ]
}
```

The remaining global settings control brightness, reconnect timing, logging,
placeholder rendering and the CDU bitmap font.

## Font handling

PMWinWingBridge uses a CDU bitmap font through mcdu-dotnet.

The configured font is loaded locally and uploaded to each PFP-3N every time
that physical CDU connects or reconnects. This prevents the hardware from
falling back to a font that renders Project Magenta placeholder characters
incorrectly.

By default the bridge uses the B612 21x31 font resource used with the CDUHUB
ecosystem.

If automatic font retrieval is blocked, run:

```text
FONT_HERUNTERLADEN.bat
```

## Automatic reconnect

Each configured CDU is independent.

If USB is disconnected:

1. that role is marked disconnected,
2. the other roles continue,
3. PMWinWingBridge searches for the same configured physical device,
4. the font is uploaded again,
5. brightness is restored,
6. the last Project Magenta display is redrawn.

If CDU.exe / RCDU.exe or the virtual COM endpoint disappears, the bridge
continues retrying until the connection becomes available again.

## Building from source

Install the .NET 8 SDK, then run:

```text
2_RELEASE_BAUEN.bat
```

The script creates:

```text
publish\PMWinWingBridge.exe
publish\bridge-config.json
publish\README.md
publish\LICENSE
publish\CHANGELOG.md
publish\THIRD_PARTY_NOTICES.md
publish\licenses\
```

The default release is framework-dependent and requires a compatible .NET 8
runtime on the target PC.

For normal testing from source:

```text
1_STARTEN.bat
```

## Acknowledgements

PMWinWingBridge uses the excellent open-source **mcdu-dotnet** library from
the **CDUHUB** project.

The CDUHUB repository describes mcdu-dotnet as a .NET Standard 2.0 library
that contains low-level code for reading and controlling supported WinWing
devices over USB.

mcdu-dotnet is published under the BSD 3-Clause License and the NuGet package
lists:

```text
Copyright © 2025 onwards, Andrew Whewell
```

Special thanks to **Andrew Whewell and all CDUHUB contributors** for making
the library and source code available to the flight-simulation community.

See `THIRD_PARTY_NOTICES.md` and `licenses/` for details.

## License

PMWinWingBridge is licensed under the **BSD 3-Clause License**.

```text
Copyright (c) 2026, Robert Kossakowski
All rights reserved.
```

This means the software can be used, modified and redistributed in source or
binary form, including commercially, provided the BSD 3-Clause conditions are
followed and the required copyright/license notices are retained.

See `LICENSE` for the complete license text.

## Disclaimer

This software is provided **"as is"**, without warranty of any kind.

Use of PMWinWingBridge is entirely at your own risk. The copyright holder and
contributors are not liable for direct or indirect damage, data loss,
equipment malfunction, simulator malfunction, interruption of operation or
other consequences arising from use of the software.

PMWinWingBridge is an independent community project. Project Magenta, WinWing,
CDUHUB, mcdu-dotnet and other product/project names remain the property of
their respective owners. No endorsement by those parties is implied.

## Credits

**PMWinWingBridge**  
Copyright © 2026 Robert Kossakowski

Developed with assistance from OpenAI ChatGPT.
## Feedback, bug reports and feature requests

Community feedback is welcome.

- **Reproducible bugs:** open a GitHub Issue and use the **Bug report** form.
- **Feature requests:** open a GitHub Issue and use the **Feature request** form.
- **Questions, setup help and general feedback:** use GitHub Discussions.

When reporting USB, serial, display or LED problems, please include the
relevant portion of `Logs/bridge-*.log` and describe the Project Magenta /
virtual-COM configuration.

See [SUPPORT.md](SUPPORT.md) for reporting guidelines.
