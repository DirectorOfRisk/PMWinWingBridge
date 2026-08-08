using System.Collections;
using System.Reflection;
using McduDotNet;

namespace PMWinWingBridge;

internal sealed record WinWingUsbCandidate(
    string Identity,
    string Description,
    string IdentityKind,
    object Descriptor
);

internal static class WinWingDeviceDiscovery
{
    private static readonly string[] SerialNames =
    {
        "SerialNumber", "Serial", "UsbSerial", "DeviceSerial", "SerialNo"
    };

    private static readonly string[] PathNames =
    {
        "DevicePath", "Path", "HidPath", "UsbPath"
    };

    public static IReadOnlyList<WinWingUsbCandidate> FindPfp3N()
    {
        var result = new List<WinWingUsbCandidate>();

        try
        {
            var method = typeof(CduFactory).GetMethod(
                "FindLocalDevices",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method is null)
                return result;

            if (method.Invoke(null, null) is not IEnumerable devices)
                return result;

            foreach (var descriptor in devices.Cast<object>())
            {
                var dump = DescribeProperties(descriptor);

                // The V1 factory enumerates CDU USB devices. If a descriptor exposes
                // a product/device property then explicitly filter to PFP-3N.
                var productText = GetFirstPropertyString(
                    descriptor,
                    "Device", "Product", "DeviceType", "ProductType", "Model"
                );

                if (!string.IsNullOrWhiteSpace(productText) &&
                    !Normalize(productText).Contains("PFP3N") &&
                    !Normalize(productText).Contains("WINWINGPFP3N"))
                {
                    continue;
                }

                var serial = GetFirstPropertyString(descriptor, SerialNames);
                var path = GetFirstPropertyString(descriptor, PathNames);

                string identity;
                string kind;
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    identity = "SERIAL:" + serial.Trim();
                    kind = "USB serial";
                }
                else if (!string.IsNullOrWhiteSpace(path))
                {
                    identity = "PATH:" + path.Trim();
                    kind = "device path (fallback)";
                }
                else
                {
                    // Last-resort descriptor fingerprint. This is intentionally
                    // marked as fallback because it may not be stable.
                    identity = "DESC:" + dump;
                    kind = "descriptor fingerprint (fallback)";
                }

                result.Add(new WinWingUsbCandidate(
                    identity,
                    string.IsNullOrWhiteSpace(dump) ? descriptor.ToString() ?? "PFP-3N" : dump,
                    kind,
                    descriptor
                ));
            }
        }
        catch
        {
            // Caller treats empty result as "no device / API could not enumerate".
        }

        return result
            .GroupBy(x => x.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public static WinWingUsbCandidate? FindByIdentity(string identity)
        => FindPfp3N().FirstOrDefault(x =>
            x.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase));

    public static ICdu? ConnectSpecific(WinWingUsbCandidate candidate)
    {
        var factory = typeof(CduFactory);
        var descriptor = candidate.Descriptor;
        var descriptorType = descriptor.GetType();

        // Try every public ConnectLocal overload and bind the USB descriptor
        // or a serial/path filter. This keeps the bridge compatible with
        // mcdu-dotnet V1 variants without guessing a single signature.
        foreach (var method in factory.GetMethods(BindingFlags.Public | BindingFlags.Static)
                     .Where(m => m.Name == "ConnectLocal"))
        {
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];
            var boundIdentity = false;
            var valid = true;

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var pt = p.ParameterType;

                if (pt.IsAssignableFrom(descriptorType))
                {
                    args[i] = descriptor;
                    boundIdentity = true;
                    continue;
                }

                if (pt == typeof(string))
                {
                    var pn = Normalize(p.Name ?? "");
                    if (pn.Contains("SERIAL") && candidate.Identity.StartsWith("SERIAL:", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = candidate.Identity["SERIAL:".Length..];
                        boundIdentity = true;
                        continue;
                    }
                    if ((pn.Contains("PATH") || pn.Contains("DEVICE")) &&
                        candidate.Identity.StartsWith("PATH:", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] = candidate.Identity["PATH:".Length..];
                        boundIdentity = true;
                        continue;
                    }
                }

                if (pt.IsEnum && Normalize(p.Name ?? "").Contains("DEVICE"))
                {
                    try
                    {
                        args[i] = Enum.Parse(pt, "WinWingPfp3N", ignoreCase: true);
                        continue;
                    }
                    catch { }
                }

                if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue;
                    continue;
                }

                valid = false;
                break;
            }

            if (!valid || !boundIdentity)
                continue;

            try
            {
                if (method.Invoke(null, args) is ICdu cdu)
                    return cdu;
            }
            catch
            {
                // Try another overload.
            }
        }

        // Safe fallback only when exactly one matching PFP is currently attached.
        // We never use "first device" fallback when several devices are present,
        // because that could swap Captain and F/O.
        if (FindPfp3N().Count == 1)
        {
            try
            {
                return CduFactory.ConnectLocal(device: Device.WinWingPfp3N);
            }
            catch { }
        }

        return null;
    }

    private static string GetFirstPropertyString(object obj, params string[] names)
    {
        var type = obj.GetType();
        foreach (var name in names)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null || !prop.CanRead) continue;
            try
            {
                var value = prop.GetValue(obj)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            catch { }
        }
        return "";
    }

    private static string DescribeProperties(object obj)
    {
        var parts = new List<string>();
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            try
            {
                var value = p.GetValue(obj)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{p.Name}={value}");
            }
            catch { }
        }
        return string.Join("; ", parts);
    }

    private static string Normalize(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
