using System;
using Avalonia.Media;

namespace XnaFontTextureGenerator.Model;

public class CombinedFontStyle
{
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