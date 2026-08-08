namespace PMWinWingBridge;

internal static class PmKeyMapper
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        // Project Magenta function keys
        ["INITREF"] = "INIT REF",
        ["INITREFERENCE"] = "INIT REF",
        ["RTE"] = "RTE",
        ["ROUTE"] = "RTE",
        ["CLB"] = "CLB",
        ["CLIMB"] = "CLB",
        ["CRZ"] = "CRZ",
        ["CRUISE"] = "CRZ",
        ["DES"] = "DES",
        ["DESCENT"] = "DES",
        ["MENU"] = "MENU",
        ["LEGS"] = "LEGS",
        ["DEPARR"] = "DEP ARR",
        ["DEPARTARRIVE"] = "DEP ARR",
        ["DEPARTUREARRIVAL"] = "DEP ARR",
        ["HOLD"] = "HOLD",
        ["PROG"] = "PROG",
        ["PROGRESS"] = "PROG",
        ["EXEC"] = "EXEC",
        ["EXECUTE"] = "EXEC",
        ["N1"] = "N1 LIMIT",
        ["N1LIMIT"] = "N1 LIMIT",
        ["FIX"] = "FIX",
        ["PREVPAGE"] = "PREV PAGE",
        ["PREVIOUSPAGE"] = "PREV PAGE",
        ["NEXT"] = "NEXT PAGE",
        ["NEXTPAGE"] = "NEXT PAGE",

        // Edit / special
        ["CLR"] = "CLR",
        ["CLEAR"] = "CLR",
        ["DEL"] = "DEL",
        ["DELETE"] = "DEL",
        ["SP"] = "SP",
        ["SPACE"] = "SP",
        ["PLUSMINUS"] = "+/-",
        ["POSITIVENEGATIVE"] = "+/-",
        ["PLUSORMINUS"] = "+/-",
        ["SIGN"] = "+/-",
        ["DOT"] = ".",
        ["PERIOD"] = ".",
        ["DECIMAL"] = ".",
        ["DECIMALPOINT"] = ".",
        ["SLASH"] = "/",

        // LSK left
        ["LS1"] = "LS1", ["LS2"] = "LS2", ["LS3"] = "LS3",
        ["LS4"] = "LS4", ["LS5"] = "LS5", ["LS6"] = "LS6",
        ["LSK1L"] = "LS1", ["LSK2L"] = "LS2", ["LSK3L"] = "LS3",
        ["LSK4L"] = "LS4", ["LSK5L"] = "LS5", ["LSK6L"] = "LS6",
        ["LEFT1"] = "LS1", ["LEFT2"] = "LS2", ["LEFT3"] = "LS3",
        ["LEFT4"] = "LS4", ["LEFT5"] = "LS5", ["LEFT6"] = "LS6",
        ["LINESELECTLEFT1"] = "LS1", ["LINESELECTLEFT2"] = "LS2", ["LINESELECTLEFT3"] = "LS3",
        ["LINESELECTLEFT4"] = "LS4", ["LINESELECTLEFT5"] = "LS5", ["LINESELECTLEFT6"] = "LS6",

        // LSK right
        ["RS1"] = "RS1", ["RS2"] = "RS2", ["RS3"] = "RS3",
        ["RS4"] = "RS4", ["RS5"] = "RS5", ["RS6"] = "RS6",
        ["LSK1R"] = "RS1", ["LSK2R"] = "RS2", ["LSK3R"] = "RS3",
        ["LSK4R"] = "RS4", ["LSK5R"] = "RS5", ["LSK6R"] = "RS6",
        ["RIGHT1"] = "RS1", ["RIGHT2"] = "RS2", ["RIGHT3"] = "RS3",
        ["RIGHT4"] = "RS4", ["RIGHT5"] = "RS5", ["RIGHT6"] = "RS6",
        ["LINESELECTRIGHT1"] = "RS1", ["LINESELECTRIGHT2"] = "RS2", ["LINESELECTRIGHT3"] = "RS3",
        ["LINESELECTRIGHT4"] = "RS4", ["LINESELECTRIGHT5"] = "RS5", ["LINESELECTRIGHT6"] = "RS6",
    };

    public static string? ToProjectMagenta(string winWingKey)
    {
        if (string.IsNullOrWhiteSpace(winWingKey))
            return null;

        var raw = winWingKey.Trim();

        // If mcdu-dotnet emits the literal keyboard value, use it directly.
        if (raw.Length == 1)
        {
            var c = raw[0];
            if (char.IsLetter(c)) return char.ToUpperInvariant(c).ToString();
            if (char.IsDigit(c)) return c.ToString();
            if (c == '.') return ".";
            if (c == '/') return "/";
        }

        if (raw == "+/-" || raw == "±")
            return "+/-";

        var normalized = Normalize(raw);

        if (Known.TryGetValue(normalized, out var mapped))
            return mapped;

        // Common enum naming patterns: LetterA, AlphaA, KeyA, Digit1, Number1.
        foreach (var prefix in new[] { "LETTER", "ALPHA", "KEY" })
        {
            if (normalized.StartsWith(prefix) && normalized.Length == prefix.Length + 1)
            {
                var c = normalized[^1];
                if (c is >= 'A' and <= 'Z')
                    return c.ToString();
            }
        }

        foreach (var prefix in new[] { "DIGIT", "NUMBER", "NUM", "KEY" })
        {
            if (normalized.StartsWith(prefix) && normalized.Length == prefix.Length + 1)
            {
                var c = normalized[^1];
                if (char.IsDigit(c))
                    return c.ToString();
            }
        }

        return null;
    }

    private static string Normalize(string value)
        => new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
