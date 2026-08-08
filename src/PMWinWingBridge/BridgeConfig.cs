using System.Text.Json;
using System.Text.Json.Serialization;

namespace PMWinWingBridge;

internal enum CduRole
{
    Captain,
    FirstOfficer,
    Observer
}

internal sealed class CduProfile
{
    public CduRole Role { get; set; } = CduRole.Captain;

    // SERIAL:<usb serial> is preferred. PATH:<hid path> is a fallback.
    public string UsbIdentity { get; set; } = "";

    // Human-readable diagnostics; does not participate in matching.
    public string UsbDescription { get; set; } = "";

    // Bridge-side COM port. Project Magenta uses the opposite side
    // of the virtual null-modem pair.
    public string ComPort { get; set; } = "COM4";

    public int BaudRate { get; set; } = 38400;
    public bool Enabled { get; set; } = true;
}

internal sealed class BridgeConfig
{
    // Multi-CDU profiles. One role each is recommended.
    public List<CduProfile> Devices { get; set; } = new();

    public int DisplayBrightnessPercent { get; set; } = 80;
    public int BacklightBrightnessPercent { get; set; } = 70;
    public int LedBrightnessPercent { get; set; } = 70;
    public int ReconnectIntervalMs { get; set; } = 2000;
    public bool LedSelfTestOnStart { get; set; } = false;
    public bool LogKeyReleases { get; set; } = false;
    public string PlaceholderCharacter { get; set; } = "□";

    public bool FontEnabled { get; set; } = true;
    public string FontFile { get; set; } = @"Fonts\b612-font-21x31.json";
    public bool FontUseFullWidth { get; set; } = false;
    public bool FontAutoDownload { get; set; } = true;
    public string FontDownloadUrl { get; set; } =
        "https://raw.githubusercontent.com/vradarserver/cduhub/main/library/cduhub/Resources/b612-font-21x31.json";

    // Legacy single-CDU fields are retained only so an older config can
    // be migrated by the setup wizard.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComPort { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BaudRate { get; set; }

    public static BridgeConfig Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new BridgeConfig();

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<BridgeConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                }) ?? new BridgeConfig();

            config.Normalize();
            return config;
        }
        catch
        {
            return new BridgeConfig();
        }
    }

    public void Save(string path)
    {
        Normalize();
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
        File.WriteAllText(path, json);
    }

    public void Normalize()
    {
        ReconnectIntervalMs = Math.Clamp(ReconnectIntervalMs, 500, 30000);
        DisplayBrightnessPercent = Math.Clamp(DisplayBrightnessPercent, 0, 100);
        BacklightBrightnessPercent = Math.Clamp(BacklightBrightnessPercent, 0, 100);
        LedBrightnessPercent = Math.Clamp(LedBrightnessPercent, 0, 100);

        if (string.IsNullOrEmpty(PlaceholderCharacter)) PlaceholderCharacter = "□";
        if (PlaceholderCharacter.Length > 1) PlaceholderCharacter = PlaceholderCharacter[..1];
        if (string.IsNullOrWhiteSpace(FontFile)) FontFile = @"Fonts\b612-font-21x31.json";

        foreach (var profile in Devices)
        {
            if (string.IsNullOrWhiteSpace(profile.ComPort))
                profile.ComPort = "COM4";
            profile.ComPort = NormalizeComPort(profile.ComPort);
            if (profile.BaudRate <= 0)
                profile.BaudRate = 38400;
        }
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
