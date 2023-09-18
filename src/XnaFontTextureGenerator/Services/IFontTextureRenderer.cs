﻿using System.Collections.Generic;
using Avalonia.Media.Imaging;
using XnaFontTextureGenerator.Model;

namespace XnaFontTextureGenerator.Services;

public interface IFontTextureRenderer
{
    Bitmap Render(IReadOnlyList<string> chars, TextureMetadata metadata);
}