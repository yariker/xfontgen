using NeoSmart.Unicode;
using System;
using System.Globalization;
using System.Linq;
using XnaFontTextureGenerator.Model;

namespace XnaFontTextureGenerator.Services;

public class CharHelper
{
    public const string Placeholder = "\u25A1";

    public static GlyphKinds GetKind(string chr)
    {
        return Emoji.IsEmoji(chr) ? GlyphKinds.Emoji : GlyphKinds.Regular;
    }

    public static int ConvertToCode(string chr)
    {
        return chr.Length == 1 ? chr[0] : char.ConvertToUtf32(chr, 0);
    }

    public static string ConvertFromCode(int code)
    {
        return code is >= char.MinValue and <= char.MaxValue
            ? $"{(char)code}"
            : char.ConvertFromUtf32(code);
    }

    public static int ConvertToChar(string codeOrValue)
    {
        if (codeOrValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(codeOrValue[2..], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var h))
        {
            return (int)h;
        }

        if (uint.TryParse(codeOrValue, out var i))
        {
            return (int)i;
        }

        var v = codeOrValue.Length switch
        {
            1 => codeOrValue[0],
            2 => char.ConvertToUtf32(codeOrValue[0], codeOrValue[1]),
            _ => throw new ArgumentException($"Invalid character/code '{codeOrValue}'.")
        };

        return v;
    }

    public static string[] GetChars(int minChar, int maxChar)
    {
        var count = maxChar - minChar + 1;

        return count > 0
            ? Enumerable.Range(minChar, count).Select(ConvertFromCode).ToArray()
            : [];
    }
}
