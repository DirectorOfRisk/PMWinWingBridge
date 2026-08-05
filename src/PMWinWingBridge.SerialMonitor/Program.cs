using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("PMWinWingBridge – PM Serial Monitor");
Console.WriteLine("====================================");

var configPath = Path.Combine(AppContext.BaseDirectory, "serial-monitor.json");
if (!File.Exists(configPath))
{
    Console.Error.WriteLine($"Konfigurationsdatei fehlt: {configPath}");
    return 2;
}

SerialMonitorConfig? config;
try
{
    var json = await File.ReadAllTextAsync(configPath);
    config = JsonSerializer.Deserialize<SerialMonitorConfig>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Konfiguration konnte nicht gelesen werden: {ex.Message}");
    return 3;
}

if (config is null || string.IsNullOrWhiteSpace(config.PortName))
{
    Console.Error.WriteLine("Ungültige Konfiguration.");
    return 4;
}

if (!Enum.TryParse<Parity>(config.Parity, true, out var parity) ||
    !Enum.TryParse<StopBits>(config.StopBits, true, out var stopBits) ||
    !Enum.TryParse<Handshake>(config.Handshake, true, out var handshake))
{
    Console.Error.WriteLine("Parity, StopBits oder Handshake in serial-monitor.json ist ungültig.");
    return 5;
}

var availablePorts = SerialPort.GetPortNames().OrderBy(static p => p, StringComparer.OrdinalIgnoreCase).ToArray();
Console.WriteLine($"Vorhandene Ports: {(availablePorts.Length == 0 ? "keine" : string.Join(", ", availablePorts))}");
Console.WriteLine($"Öffne {config.PortName} mit {config.BaudRate} Baud, {config.DataBits}{ParityLetter(parity)}{StopBitsNumber(stopBits)} …");

var logDirectory = Path.IsPathRooted(config.LogDirectory)
    ? config.LogDirectory
    : Path.Combine(AppContext.BaseDirectory, config.LogDirectory);
Directory.CreateDirectory(logDirectory);
var logPath = Path.Combine(logDirectory, $"pm-serial-{DateTime.Now:yyyyMMdd-HHmmss}.log");

await using var log = new StreamWriter(logPath, append: false, new UTF8Encoding(false)) { AutoFlush = true };
await log.WriteLineAsync($"# PMWinWingBridge Serial Monitor");
await log.WriteLineAsync($"# Started: {DateTimeOffset.Now:O}");
await log.WriteLineAsync($"# Port: {config.PortName}, {config.BaudRate}, {config.DataBits}{ParityLetter(parity)}{StopBitsNumber(stopBits)}, handshake={handshake}");

using var port = new SerialPort(config.PortName, config.BaudRate, parity, config.DataBits, stopBits)
{
    Handshake = handshake,
    ReadBufferSize = config.ReadBufferSize,
    DtrEnable = false,
    RtsEnable = false,
    ReadTimeout = 500,
    WriteTimeout = 500
};

var sync = new object();
port.DataReceived += (_, _) =>
{
    try
    {
        while (port.BytesToRead > 0)
        {
            var count = port.BytesToRead;
            var buffer = new byte[count];
            var read = port.Read(buffer, 0, buffer.Length);
            if (read <= 0) continue;
            if (read != buffer.Length) Array.Resize(ref buffer, read);

            var timestamp = DateTime.Now;
            var hex = BitConverter.ToString(buffer).Replace("-", " ");
            var ascii = ToPrintableAscii(buffer);
            var line = $"{timestamp:HH:mm:ss.fff} RX {buffer.Length,4} | {hex} | {ascii}";

            lock (sync)
            {
                Console.WriteLine(line);
                log.WriteLine(line);
            }
        }
    }
    catch (Exception ex)
    {
        lock (sync)
        {
            var line = $"{DateTime.Now:HH:mm:ss.fff} ERROR | {ex.Message}";
            Console.Error.WriteLine(line);
            log.WriteLine(line);
        }
    }
};

try
{
    port.Open();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{config.PortName} konnte nicht geöffnet werden: {ex.Message}");
    Console.Error.WriteLine("Prüfe, ob ein anderes Programm den Port verwendet und ob COM10 ↔ COM11 aktiv ist.");
    return 6;
}

Console.WriteLine($"OK: {config.PortName} ist geöffnet.");
Console.WriteLine($"Logdatei: {logPath}");
Console.WriteLine();
Console.WriteLine("Jetzt Project Magenta CDU.exe starten. ENTER beendet den Monitor.");
Console.ReadLine();

port.Close();
await log.WriteLineAsync($"# Stopped: {DateTimeOffset.Now:O}");
Console.WriteLine("Monitor beendet.");
return 0;

static string ToPrintableAscii(byte[] data)
{
    var chars = new char[data.Length];
    for (var i = 0; i < data.Length; i++)
    {
        var b = data[i];
        chars[i] = b is >= 32 and <= 126 ? (char)b : '.';
    }
    return new string(chars);
}

static char ParityLetter(Parity parity) => parity switch
{
    Parity.None => 'N',
    Parity.Odd => 'O',
    Parity.Even => 'E',
    Parity.Mark => 'M',
    Parity.Space => 'S',
    _ => '?'
};

static string StopBitsNumber(StopBits stopBits) => stopBits switch
{
    StopBits.One => "1",
    StopBits.OnePointFive => "1.5",
    StopBits.Two => "2",
    _ => "?"
};

internal sealed class SerialMonitorConfig
{
    public string PortName { get; set; } = "COM11";
    public int BaudRate { get; set; } = 38400;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public int ReadBufferSize { get; set; } = 65536;
    public string LogDirectory { get; set; } = "logs";
}
