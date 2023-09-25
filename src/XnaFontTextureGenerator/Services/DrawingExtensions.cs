using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia;
using SkiaSharp;
using System;

namespace XnaFontTextureGenerator.Services;

public static class DrawingExtensions
{
    public static void SetRenderOptions(this DrawingContext canvas, Func<RenderOptions, RenderOptions> action)
    {
        // WORKAROUND: There's currently no public API to specify custom RenderOptions for DrawingContext.
        // https://github.com/AvaloniaUI/Avalonia/issues/2464
        var propertyInfo = canvas.GetType().GetProperty("RenderOptions")!;
        var renderOptions = (RenderOptions)propertyInfo.GetValue(canvas)!;
        propertyInfo.SetValue(canvas, action(renderOptions));
    }

    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var result = new SKBitmap(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), result.GetPixels(), result.ByteCount, result.RowBytes);
        return result;
    }
}