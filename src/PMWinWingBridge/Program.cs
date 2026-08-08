using System.IO.Ports;
using PMWinWingBridge;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "PM-WinWing Bridge – Multi-CDU v1.0.0";

Console.WriteLine("PM-WinWing Bridge – Multi-CDU v1.0.0");
Console.WriteLine("======================================");
Console.WriteLine("Captain / First Officer / Observer");
Console.WriteLine();

var workingDir = Directory.GetCurrentDirectory();
var configPath = Path.Combine(workingDir, "bridge-config.json");
var logDir = Path.Combine(workingDir, "Logs");
var logPath = Path.Combine(logDir, $"bridge-{DateTime.Now:yyyyMMdd-HHmmss}.log");
Directory.CreateDirectory(logDir);

var logLock = new object();
void Log(string text)
{
    var line = $"{DateTime.Now:HH:mm:ss.fff} {text}";
    Console.WriteLine(line);
    lock (logLock)
        File.AppendAllText(logPath, line + Environment.NewLine);
}

var config = BridgeConfig.Load(configPath);
var setupRequested = args.Any(a => a.Equals("--setup", StringComparison.OrdinalIgnoreCase));

// Migrate older single-CDU config by asking the user instead of silently
// deciding whether it is Captain, F/O or Observer.
if (setupRequested || config.Devices.Count == 0)
{
    Console.WriteLine(config.Devices.Count == 0
        ? "Noch keine feste CDU-Zuordnung vorhanden."
        : "Device-Setup wurde manuell gestartet.");

    if (!DeviceSetupWizard.Run(config, configPath, Log))
    {
        Console.WriteLine();
        Console.WriteLine("Keine aktiven CDU-Profile. Programm wird beendet.");
        return;
    }

    config = BridgeConfig.Load(configPath);
}

config.Normalize();
config.Save(configPath);

var enabled = config.Devices.Where(x => x.Enabled).ToList();
if (enabled.Count == 0)
{
    Console.WriteLine("Keine aktivierten CDU-Profile.");
    Console.WriteLine("SETUP_DEVICES.bat ausführen.");
    return;
}

Console.WriteLine("Konfigurierte CDUs:");
foreach (var p in enabled.OrderBy(x => x.Role))
{
    Console.WriteLine(
        $"  {DeviceSetupWizard.RoleName(p.Role),-14} | {p.ComPort,-6} | {p.UsbIdentity}"
    );
}
Console.WriteLine();

var duplicateRole = enabled.GroupBy(x => x.Role).FirstOrDefault(g => g.Count() > 1);
if (duplicateRole is not null)
{
    Console.WriteLine($"FEHLER: Rolle {duplicateRole.Key} ist mehrfach konfiguriert.");
    Console.WriteLine("SETUP_DEVICES.bat ausführen.");
    return;
}

var duplicateCom = enabled.GroupBy(x => x.ComPort, StringComparer.OrdinalIgnoreCase)
    .FirstOrDefault(g => g.Count() > 1);
if (duplicateCom is not null)
{
    Console.WriteLine($"FEHLER: COM-Port {duplicateCom.Key} wird von mehreren Rollen verwendet.");
    Console.WriteLine("Jede PM-CDU braucht ihr eigenes virtuelles COM-Paar.");
    return;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

_ = Task.Run(() =>
{
    try { Console.ReadLine(); } catch { }
    shutdown.Cancel();
});

var configuredFont = await CduFontManager.LoadConfiguredFontAsync(
    config,
    workingDir,
    Log,
    shutdown.Token
);

Console.WriteLine("Ports: " + string.Join(", ", SerialPort.GetPortNames().OrderBy(x => x)));
Console.WriteLine("ENTER oder Ctrl+C beendet alle Bridge-Sessions.");
Console.WriteLine();

var sessions = enabled.Select(profile =>
    new BridgeSession(profile, config, configuredFont, logPath, Log)
).ToList();

try
{
    var tasks = sessions.Select(x => x.RunAsync(shutdown.Token)).ToArray();
    await Task.WhenAll(tasks);
}
finally
{
    foreach (var session in sessions)
        await session.DisposeAsync();
}

Log("SHUTDOWN COMPLETE");
