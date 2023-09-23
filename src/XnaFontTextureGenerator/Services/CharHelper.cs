using System;
using System.Globalization;
using System.Linq;

namespace XnaFontTextureGenerator.Services;

public class CharHelper
{
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
            : Array.Empty<string>();
    }
}