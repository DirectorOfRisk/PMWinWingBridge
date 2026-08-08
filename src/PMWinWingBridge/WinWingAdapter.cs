using McduDotNet;

namespace PMWinWingBridge;

internal sealed class WinWingAdapter : IDisposable
{
    private readonly ICdu _cdu;
    private readonly object _deviceSync = new();

    public event Action<string>? KeyPressed;
    public event Action<string>? KeyReleased;
    public event Action? Disconnected;

    private WinWingAdapter(ICdu cdu)
    {
        _cdu = cdu;
        _cdu.KeyDown += (_, e) => KeyPressed?.Invoke(e.Key.ToString());
        _cdu.KeyUp += (_, e) => KeyReleased?.Invoke(e.Key.ToString());
        _cdu.Disconnected += (_, _) => Disconnected?.Invoke();
    }

    public static WinWingAdapter? Connect(string usbIdentity)
    {
        var candidate = WinWingDeviceDiscovery.FindByIdentity(usbIdentity);
        if (candidate is null)
            return null;

        var cdu = WinWingDeviceDiscovery.ConnectSpecific(candidate);
        return cdu is null ? null : new WinWingAdapter(cdu);
    }

    public static WinWingAdapter? ConnectFirstAvailable()
    {
        // Retained for one-device diagnostics/setup only.
        var cdu = CduFactory.ConnectLocal(device: Device.WinWingPfp3N);
        return cdu is null ? null : new WinWingAdapter(cdu);
    }


    public bool ApplyFont(McduFontFile? font, bool useFullWidth, Action<string>? log = null)
    {
        if (font is null)
            return false;

        lock (_deviceSync)
        {
            try
            {
                // Force the upload on every USB connect/reconnect. The PFP keeps
                // display state in hardware, but a newly enumerated device must
                // not be assumed to still have the previous custom font.
                _cdu.UseFont(
                    font,
                    useFullWidth: useFullWidth
                );

                log?.Invoke($"FONT UPLOAD | OK | fullWidth={useFullWidth}");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"FONT UPLOAD WARNUNG | {ex.GetBaseException().Message}");
                return false;
            }
        }
    }

    public void ApplyBrightness(BridgeConfig config)
    {
        lock (_deviceSync)
        {
            _cdu.DisplayBrightnessPercent = config.DisplayBrightnessPercent;
            _cdu.BacklightBrightnessPercent = config.BacklightBrightnessPercent;
            _cdu.LedBrightnessPercent = config.LedBrightnessPercent;
            _cdu.RefreshBrightnesses();
        }
    }

    public void ShowBridgeReady(string comPort, int baudRate)
    {
        lock (_deviceSync)
        {
            _cdu.Output
                .Clear()
                .White().Large().Centered("PM-WINWING")
                .NewLine().Green().Small().Centered("MULTI CDU 0.4.0")
                .NewLine().NewLine()
                .White().Small().Centered("PFP-3N VERBUNDEN")
                .NewLine().NewLine()
                .Cyan().Small().Centered($"PM LINK {comPort}")
                .NewLine().Cyan().Small().Centered($"{baudRate} BAUD")
                .NewLine().NewLine()
                .Yellow().Small().Centered("WARTE AUF PM DISPLAY");
            _cdu.RefreshDisplay(skipDuplicateCheck: true);
        }
    }

    public void RenderPmDisplay(PmCduSnapshot state)
    {
        lock (_deviceSync)
        {
            var output = _cdu.Output;
            output.Clear();

            // 14 Boeing CDU display lines:
            // title, six pairs of small/large lines, scratchpad.
            output.White().Large().Centered(Pair24(state.Title, state.Page));

            for (var i = 0; i < 6; i++)
            {
                output.NewLine().White().Small().Centered(Pair24(state.LeftSmall[i], state.RightSmall[i]));
                output.NewLine().White().Large().Centered(Pair24(state.LeftText[i], state.RightText[i]));
            }

            output.NewLine().White().Large().Centered(Left24(state.Scratchpad));

            _cdu.RefreshDisplay();
        }
    }

    public bool SetPmLed(string pmLedName, bool on, Action<string>? log = null)
    {
        lock (_deviceSync)
        {
            try
            {
#pragma warning disable CS0618 // V1 mcdu-dotnet API used deliberately for package 1.4.0
                switch (pmLedName.ToUpperInvariant())
                {
                    case "EXEC":
                        _cdu.Leds.Exec = on;
                        break;
                    case "MSG":
                        _cdu.Leds.Msg = on;
                        break;
                    case "FAIL":
                        _cdu.Leds.Fail = on;
                        break;
                    case "DSPY":
                        _cdu.Leds.Dspy = on;
                        break;
                    case "OFST":
                        _cdu.Leds.Ofst = on;
                        break;
                    default:
                        log?.Invoke($"LED UNBEKANNT | PM={pmLedName}");
                        return false;
                }

                // IMPORTANT: RefreshLeds has a bool parameter. The previous
                // reflection implementation searched for a parameterless
                // method, so the buffer changed but no USB LED packet was sent.
                _cdu.RefreshLeds(skipDuplicateCheck: true);
#pragma warning restore CS0618

                log?.Invoke($"WINWING LED SENT | {pmLedName}={(on ? 1 : 0)}");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"LED ERROR | {pmLedName} | {ex.GetBaseException().Message}");
                return false;
            }
        }
    }

    public void RunLedSelfTest(Action<string>? log = null)
    {
        lock (_deviceSync)
        {
            try
            {
#pragma warning disable CS0618
                _cdu.Leds.Exec = true;
                _cdu.Leds.Msg = true;
                _cdu.Leds.Fail = true;
                _cdu.Leds.Dspy = true;
                _cdu.Leds.Ofst = true;
                _cdu.RefreshLeds(skipDuplicateCheck: true);
#pragma warning restore CS0618

                log?.Invoke("LED SELFTEST | EXEC MSG FAIL DSPY OFST = ON");
                Thread.Sleep(650);

#pragma warning disable CS0618
                _cdu.Leds.Exec = false;
                _cdu.Leds.Msg = false;
                _cdu.Leds.Fail = false;
                _cdu.Leds.Dspy = false;
                _cdu.Leds.Ofst = false;
                _cdu.RefreshLeds(skipDuplicateCheck: true);
#pragma warning restore CS0618

                log?.Invoke("LED SELFTEST | ALL = OFF");
            }
            catch (Exception ex)
            {
                log?.Invoke($"LED SELFTEST ERROR | {ex.GetBaseException().Message}");
            }
        }
    }

    private static string Pair24(string left, string right)
    {
        left = Sanitize(left).TrimEnd();
        right = Sanitize(right).TrimStart();

        const int width = 24;
        if (left.Length > width) left = left[..width];
        if (right.Length > width) right = right[^width..];

        var spaces = width - left.Length - right.Length;
        if (spaces < 1 && right.Length > 0)
        {
            var maxLeft = Math.Max(0, width - right.Length - 1);
            left = left.Length > maxLeft ? left[..maxLeft] : left;
            spaces = width - left.Length - right.Length;
        }

        if (spaces < 0) spaces = 0;
        var result = left + new string(' ', spaces) + right;
        return result.Length < width ? result.PadRight(width) : result[..width];
    }

    private static string Left24(string value)
    {
        var text = Sanitize(value);
        if (text.Length > 24) text = text[..24];
        return text.PadRight(24);
    }

    private static string Sanitize(string value)
        => (value ?? "").Replace('\0', ' ');

    public void Dispose()
    {
        try { _cdu.Cleanup(); } catch { }
        _cdu.Dispose();
    }
}
