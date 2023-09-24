using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using XnaFontTextureGenerator.Model;

namespace XnaFontTextureGenerator.Views;

public static class Converters
{
    public static readonly FuncValueConverter<IEnumerable<object>?, string?> ValidationErrorConverter = new(
        x => x?.Cast<Exception>().FirstOrDefault()?.Message.TrimEnd('.'));

    public static readonly FuncValueConverter<PointerEventArgs, PixelPoint?> MousePointConverter = new(
        args =>
        {
            if (args?.Source is Visual visual && TopLevel.GetTopLevel(visual) is TopLevel topLevel)
            {
                var position = args.GetPosition(visual);
                return PixelPoint.FromPoint(position, topLevel.RenderScaling);
            }

            return null;
        });

    public static readonly IValueConverter KerningConverter = new AutoInt32Converter(TextureMetadata.AutomaticKerning);

    private class AutoInt32Converter : IValueConverter
    {
        private const string Auto = "Auto";
        private readonly int _auto;

        public AutoInt32Converter(int auto) => _auto = auto;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return System.Convert.ToInt32(value, culture) == _auto ? _auto : System.Convert.ToDouble(value, culture);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var i = System.Convert.ToInt32(value, culture);
            return i == _auto ? Auto : i.ToString(culture);
        }
    }
}