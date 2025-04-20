using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace XnaFontTextureGenerator.Model;

public class CombinedFontStyle
{
    public static readonly IReadOnlyList<CombinedFontStyle> Styles =
    [
        new CombinedFontStyle("Regular", FontWeight.Normal, FontStretch.Normal, FontStyle.Normal),
        new CombinedFontStyle("Italic", FontWeight.Normal, FontStretch.Normal, FontStyle.Italic),
        new CombinedFontStyle("Semi Bold", FontWeight.SemiBold, FontStretch.Normal, FontStyle.Normal),
        new CombinedFontStyle("Semi Bold, Italic", FontWeight.SemiBold, FontStretch.Normal, FontStyle.Italic),
        new CombinedFontStyle("Bold", FontWeight.Bold, FontStretch.Normal, FontStyle.Normal),
        new CombinedFontStyle("Bold, Italic", FontWeight.Bold, FontStretch.Normal, FontStyle.Italic)
    ];

    public CombinedFontStyle(string name, FontWeight fontWeight, FontStretch fontStretch, FontStyle fontStyle)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        FontWeight = fontWeight;
        FontStretch = fontStretch;
        FontStyle = fontStyle;
    }

    public string Name { get; }

    public FontWeight FontWeight { get; }

    public FontStretch FontStretch { get; }

    public FontStyle FontStyle { get; }

    public bool IsMatch(TextureMetadata metadata)
    {
        return FontWeight == metadata.FontWeight &&
               FontStretch == metadata.FontStretch &&
               FontStyle == metadata.FontStyle;
    }

    public override string ToString() => Name;
}
