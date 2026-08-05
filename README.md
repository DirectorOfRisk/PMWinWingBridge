# PMWinWingBridge

Bridge between Project Magenta CDU software and WinWing PFP CDU hardware.

## Current milestone

The first tool is a byte-accurate serial monitor for observing Project Magenta's Engravity CDU traffic through a virtual COM pair.

- Project Magenta: `COM10` (`EngravityComm=10`)
- Serial Monitor: `COM11`
- Serial parameters: `38400 baud, 8N1, no flow control`

See [docs/01-serial-capture.md](docs/01-serial-capture.md).
