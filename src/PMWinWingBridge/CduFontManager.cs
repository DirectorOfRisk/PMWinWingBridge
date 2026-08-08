using System.Net.Http;
using System.Text.Json;
using McduDotNet;

namespace PMWinWingBridge;

internal static class CduFontManager
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static async Task<McduFontFile?> LoadConfiguredFontAsync(
        BridgeConfig config,
        string workingDir,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (!config.FontEnabled)
        {
            log?.Invoke("FONT | deaktiviert");
            return null;
        }

        var fontPath = config.FontFile;
        if (!Path.IsPathRooted(fontPath))
            fontPath = Path.Combine(workingDir, fontPath);

        try
        {
            if (!File.Exists(fontPath) && config.FontAutoDownload)
            {
                var dir = Path.GetDirectoryName(fontPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                log?.Invoke($"FONT | lade einmalig {Path.GetFileName(fontPath)} von CDUHUB ...");
                using var response = await Http.GetAsync(config.FontDownloadUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                // Avoid caching an HTML/error page as a font.
                var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 32)).TrimStart();
                if (!prefix.StartsWith("{") && !prefix.StartsWith("["))
                    throw new InvalidDataException("Download ist keine JSON-Fontdatei.");

                await File.WriteAllBytesAsync(fontPath, bytes, cancellationToken);
                log?.Invoke($"FONT | gespeichert: {fontPath}");
            }

            if (!File.Exists(fontPath))
            {
                log?.Invoke($"FONT WARNUNG | Datei fehlt: {fontPath}");
                return null;
            }

            var json = await File.ReadAllTextAsync(fontPath, cancellationToken);
            var font = JsonSerializer.Deserialize<McduFontFile>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    IncludeFields = true
                });

            if (font is null)
                throw new InvalidDataException("Fontdatei konnte nicht gelesen werden.");

            log?.Invoke($"FONT | geladen: {Path.GetFileName(fontPath)}");
            return font;
        }
        catch (Exception ex)
        {
            log?.Invoke($"FONT WARNUNG | {ex.GetBaseException().Message}");
            return null;
        }
    }
}
