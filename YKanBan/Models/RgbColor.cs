namespace YKanBan.Models;

/// <summary>
/// An RGB color without an alpha channel, stored as one byte per component
/// — mirroring the color structures of WinForms, WPF and Avalonia minus
/// the alpha byte. The type is total: every combination of components is
/// valid, and all validation happens at the text boundary. Converts to and
/// from the persisted "#RRGGBB" text form; ToString always produces the
/// canonical lowercase form (e.g. "#ff0a00").
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>Parses a "#RRGGBB" text with case-insensitive hex digits.</summary>
    /// <exception cref="FormatException">The text is not in #RRGGBB form.</exception>
    public static RgbColor Parse(string text) =>
        TryParse(text, out RgbColor color)
            ? color
            : throw new FormatException($"Not a valid #RRGGBB color: '{text}'.");

    /// <summary>Attempts to parse a "#RRGGBB" text; no whitespace tolerance.</summary>
    public static bool TryParse(string? text, out RgbColor color)
    {
        color = default;
        if (text is not { Length: 7 } || text[0] != '#')
        {
            return false;
        }

        // Two hex digits per component: R, G, B.
        if (TryHexPair(text, 1, out byte r) && TryHexPair(text, 3, out byte g) && TryHexPair(text, 5, out byte b))
        {
            color = new RgbColor(r, g, b);
            return true;
        }
        return false;
    }

    /// <summary>Returns the canonical lowercase "#RRGGBB" form used by the database and exports.</summary>
    public override string ToString() => $"#{R:x2}{G:x2}{B:x2}";

    /// <summary>Parses exactly two hex digits into a byte value.</summary>
    private static bool TryHexPair(string text, int start, out byte value)
    {
        value = 0;
        int high = HexDigit(text[start]);
        int low = HexDigit(text[start + 1]);
        if (high < 0 || low < 0)
        {
            return false;
        }
        value = (byte)((high << 4) | low);
        return true;
    }

    /// <summary>Converts one hex digit to its value; -1 for any other character.</summary>
    private static int HexDigit(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'f' => character - 'a' + 10,
        >= 'A' and <= 'F' => character - 'A' + 10,
        _ => -1,
    };
}
