using Avalonia.Media.Imaging;
using Avalonia;
using SkiaSharp;

namespace XnaFontTextureGenerator.Services;

public static class DrawingExtensions
{
    public static SKBitmap ToSKBitmap(this Bitmap bitmap)
    {
        var result = new SKBitmap(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), result.GetPixels(), result.ByteCount, result.RowBytes);
        return result;
    }
}
