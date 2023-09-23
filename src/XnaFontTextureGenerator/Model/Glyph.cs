using Avalonia;
using Avalonia.Media;

namespace XnaFontTextureGenerator.Model;

public struct Glyph
{
    public Rect Rect { get; set; }

    public Point Offset { get; set; }

    public Geometry Geometry { get; set; }
}