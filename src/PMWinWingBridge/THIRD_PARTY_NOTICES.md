# Third-Party Notices

PMWinWingBridge depends on third-party software. Copyright and license rights
for those components remain with their respective authors.

## mcdu-dotnet / CDUHUB

PMWinWingBridge uses **mcdu-dotnet 1.4.0** to communicate with supported
WinWing CDU hardware over USB/HID.

- Project: CDUHUB / mcdu-dotnet
- Author / copyright shown by the NuGet package: Copyright © 2025 onwards, Andrew Whewell
- License: BSD 3-Clause License
- Source: https://github.com/vradarserver/cduhub
- NuGet: https://www.nuget.org/packages/mcdu-dotnet/

The upstream repository describes `mcdu-dotnet` as a .NET Standard 2.0
library containing the low-level code for reading and controlling supported
WinWing devices over USB.

A copy of the BSD 3-Clause notice used for mcdu-dotnet is included in:

`licenses/MCDU-DOTNET-BSD-3-CLAUSE.txt`

Special thanks to Andrew Whewell and all CDUHUB contributors for making the
source code and library available to the flight-simulation community.

## Other NuGet dependencies

The build also restores packages required by `mcdu-dotnet` and by the .NET
serial-port implementation. Those packages remain subject to their own
licenses and copyright notices. When redistributing a binary build, retain
all third-party license files/notices that are shipped with or required by
those dependencies.
