namespace PMWinWingBridge;

[Flags]
internal enum PmChange
{
    None = 0,
    Display = 1,
    Led = 2
}

internal sealed class PmCduSnapshot
{
    public string Title { get; init; } = "";
    public string Page { get; init; } = "";
    public string Scratchpad { get; init; } = "";
    public string[] LeftSmall { get; init; } = new string[6];
    public string[] RightSmall { get; init; } = new string[6];
    public string[] LeftText { get; init; } = new string[6];
    public string[] RightText { get; init; } = new string[6];
}

internal sealed class PmCduState
{
    private readonly object _sync = new();
    private readonly char _placeholderCharacter;

    public PmCduState(string? placeholderCharacter = null)
    {
        _placeholderCharacter = string.IsNullOrEmpty(placeholderCharacter) ? '□' : placeholderCharacter[0];
    }

    private string _title = "";
    private string _page = "";
    private string _scratchpad = "";
    private readonly string[] _leftSmall = new string[6];
    private readonly string[] _rightSmall = new string[6];
    private readonly string[] _leftText = new string[6];
    private readonly string[] _rightText = new string[6];

    public PmChange Apply(string sentence, out string? ledName, out bool ledState)
    {
        ledName = null;
        ledState = false;

        if (!TryGetBody(sentence, out var body))
            return PmChange.None;

        if (body.StartsWith("PCDUSID,", StringComparison.Ordinal))
        {
            var parts = body.Split(',', 3);
            if (parts.Length == 3 && (parts[2] == "0" || parts[2] == "1"))
            {
                ledName = parts[1].Trim().ToUpperInvariant();
                ledState = parts[2] == "1";
                return PmChange.Led;
            }
            return PmChange.None;
        }

        // Normal PM display sentence:
        // PCDUTIT,F3,S,,POS INIT
        // Split only four commas so text itself may contain commas.
        var fields = body.Split(',', 5);
        if (fields.Length < 5)
            return PmChange.None;

        var command = fields[0];
        var value = CleanText(fields[4]);

        lock (_sync)
        {
            switch (command)
            {
                case "PCDUTIT":
                    return Set(ref _title, value) ? PmChange.Display : PmChange.None;
                case "PCDUPGE":
                    return Set(ref _page, value) ? PmChange.Display : PmChange.None;
                case "PCDUSPD":
                    return Set(ref _scratchpad, value) ? PmChange.Display : PmChange.None;
            }

            if (TryLineCommand(command, out var right, out var row, out var small))
            {
                var target = right
                    ? (small ? _rightSmall : _rightText)
                    : (small ? _leftSmall : _leftText);

                var index = row - 1;
                if (target[index] == value) return PmChange.None;
                target[index] = value;
                return PmChange.Display;
            }
        }

        return PmChange.None;
    }

    public PmCduSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new PmCduSnapshot
            {
                Title = _title,
                Page = _page,
                Scratchpad = _scratchpad,
                LeftSmall = (string[])_leftSmall.Clone(),
                RightSmall = (string[])_rightSmall.Clone(),
                LeftText = (string[])_leftText.Clone(),
                RightText = (string[])_rightText.Clone()
            };
        }
    }

    private static bool TryGetBody(string sentence, out string body)
    {
        body = "";
        if (string.IsNullOrWhiteSpace(sentence) || sentence[0] != '$')
            return false;

        var star = sentence.LastIndexOf('*');
        if (star <= 1)
            return false;

        body = sentence[1..star];

        // Validate checksum when two valid hex digits are present.
        if (star + 2 < sentence.Length)
        {
            var hex = sentence.Substring(star + 1, Math.Min(2, sentence.Length - star - 1));
            if (hex.Length == 2 && byte.TryParse(hex,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var expected))
            {
                byte actual = 0;
                foreach (var b in System.Text.Encoding.Latin1.GetBytes(body))
                    actual ^= b;

                if (actual != expected)
                    return false;
            }
        }

        return true;
    }

    private static bool TryLineCommand(string command, out bool right, out int row, out bool small)
    {
        right = false;
        row = 0;
        small = false;

        // PCDUL1S / PCDUR6T
        if (command.Length != 7 || !command.StartsWith("PCDU", StringComparison.Ordinal))
            return false;

        var side = command[4];
        if (side != 'L' && side != 'R') return false;

        if (command[5] is < '1' or > '6') return false;

        var kind = command[6];
        if (kind != 'S' && kind != 'T') return false;

        right = side == 'R';
        row = command[5] - '0';
        small = kind == 'S';
        return true;
    }

    private string CleanText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Project Magenta / Engravity uses an old single-byte character set.
        // Keep normal ASCII and the degree sign. Other high-byte glyphs are
        // typically CDU "box" placeholders. The WinWing default font renders
        // those raw legacy bytes as a solid missing-glyph rectangle, so map
        // them to a Unicode hollow square instead.
        var chars = new List<char>(value.Length);
        foreach (var ch in value)
        {
            if (ch == '\u00B0')
            {
                chars.Add('°');
            }
            else if (ch >= ' ' && ch <= '~')
            {
                chars.Add(ch);
            }
            else if (ch == '\t')
            {
                chars.Add(' ');
            }
            else
            {
                chars.Add(_placeholderCharacter);
            }
        }

        return new string(chars.ToArray());
    }

    private static bool Set(ref string target, string value)
    {
        if (target == value) return false;
        target = value;
        return true;
    }
}
