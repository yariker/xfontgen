using System;

namespace XnaFontTextureGenerator.Model;

[Flags]
public enum GlyphKinds
{
    None = 0,
    Regular = 1 << 0,
    Emoji = 1 << 1,
}