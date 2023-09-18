using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;

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
}