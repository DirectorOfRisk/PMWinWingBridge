using System.IO.Ports;
using System.Text;

namespace PMWinWingBridge;

internal sealed class SerialCapture : IDisposable
{
    private readonly SerialPort _port;
    private readonly object _sync = new();
    private readonly object _rxSync = new();
    private readonly string _logPath;
    private readonly StringBuilder _rxText = new();
    private long _packetNumber;
    private int _faultRaised;

    public event Action<long, byte[], string>? PacketReceived;
    public event Action<string>? SentenceReceived;
    public event Action<Exception>? Faulted;

    public bool IsOpen => _port.IsOpen;

    public SerialCapture(string portName, int baudRate, string logPath)
    {
        _logPath = logPath;
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500,
            DtrEnable = false,
            RtsEnable = false
        };
        _port.DataReceived += OnDataReceived;
    }

    public void Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath) ?? ".");
        File.AppendAllText(_logPath,
            $"\r\n=== SERIAL OPEN {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {_port.PortName} {_port.BaudRate} 8N1 ===\r\n");
        _port.Open();
        Interlocked.Exchange(ref _faultRaised, 0);
    }

    public bool TrySendAscii(string text)
    {
        try
        {
            if (!_port.IsOpen) return false;
            var bytes = Encoding.ASCII.GetBytes(text);
            _port.Write(bytes, 0, bytes.Length);
            WriteLog("TX", bytes);
            return true;
        }
        catch (Exception ex)
        {
            RaiseFault(ex);
            return false;
        }
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            Thread.Sleep(8);
            var available = _port.BytesToRead;
            if (available <= 0) return;

            var buffer = new byte[available];
            var read = _port.Read(buffer, 0, buffer.Length);
            if (read != buffer.Length) Array.Resize(ref buffer, read);

            var number = Interlocked.Increment(ref _packetNumber);
            var preview = ToAsciiPreview(buffer);
            WriteLog("RX", buffer, number);
            PacketReceived?.Invoke(number, buffer, preview);

            var chunk = Encoding.Latin1.GetString(buffer);
            List<string> complete = new();

            lock (_rxSync)
            {
                _rxText.Append(chunk);
                while (true)
                {
                    var current = _rxText.ToString();
                    var pos = current.IndexOf("\r\n", StringComparison.Ordinal);
                    if (pos < 0) break;

                    var line = current[..pos];
                    _rxText.Remove(0, pos + 2);
                    if (!string.IsNullOrEmpty(line)) complete.Add(line);
                }

                if (_rxText.Length > 16384) _rxText.Clear();
            }

            foreach (var line in complete)
                SentenceReceived?.Invoke(line);
        }
        catch (Exception ex)
        {
            RaiseFault(ex);
        }
    }

    private void RaiseFault(Exception ex)
    {
        lock (_sync)
            File.AppendAllText(_logPath, $"SERIAL ERROR {DateTime.Now:HH:mm:ss.fff} {ex}\r\n");

        if (Interlocked.Exchange(ref _faultRaised, 1) == 0)
            Faulted?.Invoke(ex);
    }

    private void WriteLog(string direction, byte[] bytes, long? number = null)
    {
        var hexText = Convert.ToHexString(bytes);
        var hex = string.Join(" ", Enumerable.Range(0, hexText.Length / 2)
            .Select(i => hexText.Substring(i * 2, 2)));
        var ascii = ToAsciiPreview(bytes);
        var prefix = number.HasValue ? $"#{number.Value:000000} " : string.Empty;
        var line = $"{DateTime.Now:HH:mm:ss.fff} {prefix}{direction} {bytes.Length,4} | {hex} | {ascii}\r\n";
        lock (_sync) File.AppendAllText(_logPath, line);
    }

    private static string ToAsciiPreview(byte[] bytes)
    {
        var chars = bytes.Select(b => b is >= 32 and <= 126 ? (char)b : '.').ToArray();
        return new string(chars);
    }

    public void Dispose()
    {
        try { _port.DataReceived -= OnDataReceived; } catch { }
        try { if (_port.IsOpen) _port.Close(); } catch { }
        try { _port.Dispose(); } catch { }
    }
}
