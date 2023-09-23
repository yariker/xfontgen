using System.Collections.Generic;
using System.Threading;
using Avalonia.Media.Imaging;
using XnaFontTextureGenerator.Model;

namespace XnaFontTextureGenerator.Services;

public interface IFontTextureRenderer
{
    Bitmap Render(IReadOnlyList<string> chars, TextureMetadata metadata, out Glyph[] glyphs,
        CancellationToken cancellationToken = default);
}