using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Data.Converters;

namespace XnaFontTextureGenerator.Views;

public static class Converters
{
    public static readonly FuncValueConverter<char, string> CharConverter = new(
        x => char.IsControl(x) ? "\u25A1" : x.ToString());

    public static readonly FuncValueConverter<char, string> CharToHexConverter = new(
        x => $"'{x}' 0x{(int)x:X4}");

    public static readonly FuncValueConverter<IEnumerable<object>?, string?> ValidationErrorConverter = new(
        x => x?.Cast<Exception>().FirstOrDefault()?.Message.TrimEnd('.'));
}