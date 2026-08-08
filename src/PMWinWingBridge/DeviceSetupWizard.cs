namespace PMWinWingBridge;

internal static class DeviceSetupWizard
{
    public static bool Run(BridgeConfig config, string configPath, Action<string>? log = null)
    {
        Console.WriteLine();
        Console.WriteLine("====================================================");
        Console.WriteLine(" PMWinWingBridge – DEVICE SETUP");
        Console.WriteLine("====================================================");
        Console.WriteLine();

        var discovered = WinWingDeviceDiscovery.FindPfp3N();
        if (discovered.Count == 0)
        {
            Console.WriteLine("No WinWing PFP-3N devices were detected.");
            Console.WriteLine("Connect at least one CDU and run the setup again.");
            return false;
        }

        Console.WriteLine($"Detected CDU devices: {discovered.Count}");
        Console.WriteLine();

        foreach (var d in discovered)
        {
            var existing = config.Devices.FirstOrDefault(x =>
                x.UsbIdentity.Equals(d.Identity, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine($"{RoleName(existing.Role)} already configured.");
                Console.WriteLine();
                Console.WriteLine($"USB Identity: {d.Identity}");
                Console.WriteLine($"Current Bridge COM port: {existing.ComPort}");
                Console.WriteLine();
                Console.WriteLine("Press ENTER to keep this value");
                Console.WriteLine("or enter a new COM port.");
                Console.WriteLine();
                Console.Write("COM port: ");

                var input = (Console.ReadLine() ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(input))
                {
                    existing.ComPort = NormalizeComPort(input);
                    Console.WriteLine($"Updated: {RoleName(existing.Role)} -> {existing.ComPort}");
                }
                else
                {
                    Console.WriteLine($"Kept: {RoleName(existing.Role)} -> {existing.ComPort}");
                }

                Console.WriteLine();
                continue;
            }

            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine("New WinWing PFP-3N detected.");
            Console.WriteLine();
            Console.WriteLine($"USB Identity: {d.Identity}");
            Console.WriteLine($"Identity type: {d.IdentityKind}");
            Console.WriteLine();

            if (!d.Identity.StartsWith("SERIAL:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("WARNING:");
                Console.WriteLine("No USB serial number was exposed for this device.");
                Console.WriteLine("A fallback identity will be used.");
                Console.WriteLine("For the most reliable initial assignment, connect only");
                Console.WriteLine("the CDU that you are currently configuring.");
                Console.WriteLine();
            }

            var role = AskRole();
            if (role is null)
            {
                Console.WriteLine("Device skipped.");
                Console.WriteLine();
                continue;
            }

            var oldRole = config.Devices.FirstOrDefault(x => x.Role == role.Value);
            if (oldRole is not null)
            {
                Console.WriteLine();
                Console.WriteLine($"Role {RoleName(role.Value)} is already assigned to:");
                Console.WriteLine($"  {oldRole.UsbIdentity}");
                Console.Write("Replace this assignment? [y/N]: ");

                var replace = (Console.ReadLine() ?? "").Trim();
                if (!replace.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                    !replace.Equals("j", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Device skipped.");
                    Console.WriteLine();
                    continue;
                }

                config.Devices.Remove(oldRole);
            }

            var defaultCom = SuggestCom(config, role.Value);

            Console.WriteLine();
            Console.WriteLine($"Bridge COM port for {RoleName(role.Value)}");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  Project Magenta CDU.exe  -> COM3");
            Console.WriteLine("  PMWinWingBridge          -> COM4");
            Console.WriteLine();
            Console.WriteLine("Enter the FULL Windows COM port name");
            Console.WriteLine("(for example COM4, COM6 or COM12).");
            Console.WriteLine();
            Console.WriteLine("You may also enter only the number (for example 4).");
            Console.WriteLine("It will automatically be stored as COM4.");
            Console.WriteLine();
            Console.WriteLine($"Default value: {defaultCom}");
            Console.Write("COM port: ");

            var comInput = (Console.ReadLine() ?? "").Trim();
            var com = string.IsNullOrWhiteSpace(comInput)
                ? defaultCom
                : NormalizeComPort(comInput);

            config.Devices.Add(new CduProfile
            {
                Role = role.Value,
                UsbIdentity = d.Identity,
                UsbDescription = d.Description,
                ComPort = com,
                BaudRate = 38400,
                Enabled = true
            });

            Console.WriteLine();
            Console.WriteLine($"SAVED: {RoleName(role.Value)} -> {d.Identity} -> {com}");
            Console.WriteLine();
        }

        if (config.Devices.Count > 0)
        {
            config.ComPort = null;
            config.BaudRate = null;
        }

        config.Save(configPath);
        log?.Invoke($"SETUP | {config.Devices.Count} profile(s) saved");

        Console.WriteLine();
        Console.WriteLine("====================================================");
        Console.WriteLine(" Configuration saved.");
        Console.WriteLine("====================================================");
        Console.WriteLine();
        Console.WriteLine($"File:");
        Console.WriteLine($"  {configPath}");
        Console.WriteLine();
        Console.WriteLine("You can change the COM ports or other settings later by");
        Console.WriteLine("editing bridge-config.json directly, or by running:");
        Console.WriteLine();
        Console.WriteLine("  CONFIG_BEARBEITEN.bat");
        Console.WriteLine();

        return config.Devices.Count > 0;
    }

    public static string RoleName(CduRole role) => role switch
    {
        CduRole.Captain => "Captain",
        CduRole.FirstOfficer => "First Officer",
        CduRole.Observer => "Observer",
        _ => role.ToString()
    };

    private static CduRole? AskRole()
    {
        while (true)
        {
            Console.WriteLine("Select the role for this device:");
            Console.WriteLine();
            Console.WriteLine("  1 = Captain");
            Console.WriteLine("  2 = First Officer");
            Console.WriteLine("  3 = Observer");
            Console.WriteLine("  0 = Skip");
            Console.WriteLine();
            Console.Write("Selection: ");

            switch ((Console.ReadLine() ?? "").Trim().ToUpperInvariant())
            {
                case "1":
                case "C":
                case "CAPTAIN":
                    return CduRole.Captain;

                case "2":
                case "F":
                case "FO":
                case "FIRSTOFFICER":
                case "FIRST OFFICER":
                    return CduRole.FirstOfficer;

                case "3":
                case "O":
                case "OBS":
                case "OBSERVER":
                    return CduRole.Observer;

                case "0":
                case "S":
                case "SKIP":
                    return null;
            }

            Console.WriteLine("Invalid selection.");
            Console.WriteLine();
        }
    }

    private static string SuggestCom(BridgeConfig config, CduRole role)
    {
        var preferred = role switch
        {
            CduRole.Captain => "COM4",
            CduRole.FirstOfficer => "COM6",
            CduRole.Observer => "COM8",
            _ => "COM4"
        };

        if (!config.Devices.Any(x =>
            x.ComPort.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
        {
            return preferred;
        }

        for (var n = 4; n <= 99; n++)
        {
            var candidate = $"COM{n}";
            if (!config.Devices.Any(x =>
                x.ComPort.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return preferred;
    }

    private static string NormalizeComPort(string value)
    {
        var text = (value ?? "").Trim().ToUpperInvariant();

        if (int.TryParse(text, out var number) && number > 0)
            return $"COM{number}";

        if (text.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = text[3..].Trim();
            if (int.TryParse(suffix, out number) && number > 0)
                return $"COM{number}";
        }

        return text;
    }
}
