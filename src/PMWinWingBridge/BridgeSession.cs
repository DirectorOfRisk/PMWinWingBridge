using McduDotNet;

namespace PMWinWingBridge;

internal sealed class BridgeSession : IAsyncDisposable
{
    private readonly CduProfile _profile;
    private readonly BridgeConfig _config;
    private readonly McduFontFile? _font;
    private readonly string _logPath;
    private readonly Action<string> _rootLog;
    private readonly object _sync = new();
    private readonly object _renderSync = new();

    private WinWingAdapter? _winWing;
    private SerialCapture? _serial;
    private bool _winWingFaulted;
    private bool _serialFaulted;
    private readonly PmCduState _pmState;
    private System.Threading.Timer? _renderTimer;

    public BridgeSession(
        CduProfile profile,
        BridgeConfig config,
        McduFontFile? font,
        string logPath,
        Action<string> rootLog)
    {
        _profile = profile;
        _config = config;
        _font = font;
        _logPath = logPath;
        _rootLog = rootLog;
        _pmState = new PmCduState(config.PlaceholderCharacter);
    }

    private string Role => DeviceSetupWizard.RoleName(_profile.Role);

    private void Log(string message) => _rootLog($"[{Role}] {message}");

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DateTime nextUsbAttempt = DateTime.MinValue;
        DateTime nextSerialAttempt = DateTime.MinValue;
        var lastStatus = "";

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;

            if (_winWingFaulted)
            {
                _winWingFaulted = false;
                Log("PFP-3N DISCONNECTED | Reconnect");
                DisconnectWinWing();
                nextUsbAttempt = now.AddMilliseconds(_config.ReconnectIntervalMs);
            }

            if (_serialFaulted)
            {
                _serialFaulted = false;
                Log($"PM LINK LOST | {_profile.ComPort} | Reconnect");
                DisconnectSerial();
                nextSerialAttempt = now.AddMilliseconds(_config.ReconnectIntervalMs);
            }

            if (GetWinWing() is null && now >= nextUsbAttempt)
            {
                nextUsbAttempt = now.AddMilliseconds(_config.ReconnectIntervalMs);
                try
                {
                    var candidate = WinWingAdapter.Connect(_profile.UsbIdentity);
                    if (candidate is not null)
                    {
                        candidate.Disconnected += () => _winWingFaulted = true;
                        candidate.KeyPressed += HandleKey;
                        if (_config.LogKeyReleases)
                            candidate.KeyReleased += key => Log($"KEY UP | {key}");

                        candidate.ApplyFont(_font, _config.FontUseFullWidth, msg => Log(msg));
                        candidate.ApplyBrightness(_config);
                        candidate.ShowBridgeReady(_profile.ComPort, _profile.BaudRate);

                        if (_config.LedSelfTestOnStart)
                            candidate.RunLedSelfTest(msg => Log(msg));

                        lock (_sync) _winWing = candidate;
                        Log($"PFP-3N CONNECTED | {_profile.UsbIdentity}");

                        try { candidate.RenderPmDisplay(_pmState.Snapshot()); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Log($"PFP-3N WAITING | {ex.GetBaseException().Message}");
                }
            }

            if (GetSerial() is null && now >= nextSerialAttempt)
            {
                nextSerialAttempt = now.AddMilliseconds(_config.ReconnectIntervalMs);
                try
                {
                    var candidate = new SerialCapture(_profile.ComPort, _profile.BaudRate, _logPath);
                    candidate.SentenceReceived += HandlePmSentence;
                    candidate.Faulted += _ => _serialFaulted = true;
                    candidate.Open();

                    lock (_sync) _serial = candidate;
                    Log($"PM LINK CONNECTED | {_profile.ComPort}");
                }
                catch (Exception ex)
                {
                    Log($"PM LINK WAITING | {_profile.ComPort} | {ex.GetBaseException().Message}");
                }
            }

            var usbOk = GetWinWing() is not null;
            var pmOk = GetSerial()?.IsOpen == true;
            var status = $"STATUS | USB={(usbOk ? "CONNECTED" : "WAITING")} | PM {_profile.ComPort}={(pmOk ? "CONNECTED" : "WAITING")}";
            if (status != lastStatus)
            {
                lastStatus = status;
                Log(status);
            }

            try { await Task.Delay(250, cancellationToken); }
            catch (OperationCanceledException) { }
        }

        DisconnectSerial();
        DisconnectWinWing();
    }

    private WinWingAdapter? GetWinWing()
    {
        lock (_sync) return _winWing;
    }

    private SerialCapture? GetSerial()
    {
        lock (_sync) return _serial;
    }

    private void HandleKey(string key)
    {
        var pmKey = PmKeyMapper.ToProjectMagenta(key);
        if (pmKey is null)
        {
            Log($"UNMAPPED KEY | {key}");
            return;
        }

        var serial = GetSerial();
        if (serial is null || !serial.IsOpen)
        {
            Log($"KEY WAITING | {key} -> {pmKey}");
            return;
        }

        var sentence = PmProtocol.BuildKeySentence(pmKey);
        if (!serial.TrySendAscii(sentence))
        {
            _serialFaulted = true;
            Log($"KEY TX FEHLER | {key} -> {pmKey}");
            return;
        }

        Log($"KEY | {key} -> PM={pmKey}");
    }

    private void HandlePmSentence(string sentence)
    {
        var change = _pmState.Apply(sentence, out var ledName, out var ledState);

        if ((change & PmChange.Display) != 0)
            ScheduleRender();

        if ((change & PmChange.Led) != 0 && ledName is not null)
        {
            var ww = GetWinWing();
            if (ww is not null)
                ww.SetPmLed(ledName, ledState, msg =>
                {
                    if (msg.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("FEHL", StringComparison.OrdinalIgnoreCase))
                        Log(msg);
                });
        }
    }

    private void ScheduleRender()
    {
        lock (_renderSync)
        {
            _renderTimer ??= new System.Threading.Timer(_ =>
            {
                var ww = GetWinWing();
                if (ww is null) return;
                try { ww.RenderPmDisplay(_pmState.Snapshot()); }
                catch (Exception ex) { Log($"DISPLAY ERROR | {ex.GetBaseException().Message}"); }
            });
            _renderTimer.Change(45, Timeout.Infinite);
        }
    }

    private void DisconnectWinWing()
    {
        WinWingAdapter? old;
        lock (_sync)
        {
            old = _winWing;
            _winWing = null;
        }
        try { old?.Dispose(); } catch { }
    }

    private void DisconnectSerial()
    {
        SerialCapture? old;
        lock (_sync)
        {
            old = _serial;
            _serial = null;
        }
        try { old?.Dispose(); } catch { }
    }

    public ValueTask DisposeAsync()
    {
        _renderTimer?.Dispose();
        DisconnectSerial();
        DisconnectWinWing();
        return ValueTask.CompletedTask;
    }
}
