using System.Text;

namespace PMWinWingBridge;

internal static class PmProtocol
{
    public static string BuildKeySentence(string pmKey)
        => BuildSentence($"PCDUKEY,{pmKey}");

    public static string BuildSentence(string body)
    {
        byte checksum = 0;
        foreach (var b in Encoding.ASCII.GetBytes(body))
            checksum ^= b;

        return $"${body}*{checksum:X2}\r\n";
    }
}
