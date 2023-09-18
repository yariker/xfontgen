using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using XnaFontTextureGenerator.Model;
using XnaFontTextureGenerator.Services;

namespace XnaFontTextureGenerator.Views;

public partial class MainView : UserControl, IFontTextureRenderer, IMessageBox
{
    private const double RenderDpi = 96.0;

    public MainView()
    {
        InitializeComponent();
    }

    public void Show(string message)
    {
        ErrorPopup.DataContext = message;
        ErrorPopup.IsOpen = true;
    }

    public Bitmap Render(IReadOnlyList<string> chars, TextureMetadata metadata, out Rect[] rects)
    {
        const int border = 3;
        const int minWidth = 1;
        const FlowDirection flow = FlowDirection.LeftToRight;

        var padding = metadata.DropShadow != null
            ? new Thickness(Math.Max(0, metadata.DropShadow.Blur - metadata.DropShadow.OffsetX),
                            Math.Max(0, metadata.DropShadow.Blur - metadata.DropShadow.OffsetY),
                            metadata.DropShadow.Blur + metadata.DropShadow.OffsetX,
                            metadata.DropShadow.Blur + metadata.DropShadow.OffsetY)
            : default;
        
        //
        // Measure and arrange.
        //

        var foreground = new ImmutableSolidColorBrush(metadata.Foreground);
        var typeface = new Typeface(metadata.FontName, metadata.FontStyle, metadata.FontWeight, metadata.FontStretch);
        var glyphs = new FormattedText[chars.Count];
        var culture = CultureInfo.CurrentCulture;

        var x = border;
        var y = border;
        var textureHeight = border;
        var newLine = true;

        rects = new Rect[chars.Count];

        for (var i = 0; i < chars.Count; i++)
        {
            var chr = chars[i];

            // Replace control characters with placeholder.
            if (chr.Length == 1 && char.IsControl(chr, 0))
            {
                chr = "\u25A1";
            }

            var glyph = new FormattedText(chr, culture, flow, typeface, metadata.FontSize, foreground);
            var height = (int)Math.Ceiling(glyph.Height);
            var width = (int)Math.Ceiling(Math.Max(minWidth, glyph.WidthIncludingTrailingWhitespace +
                                                             glyph.OverhangLeading +
                                                             glyph.OverhangTrailing));

            // Apply padding (if any).
            width += (int)Math.Ceiling(padding.Left + padding.Right);
            height += (int)Math.Ceiling(padding.Top + padding.Bottom);

            if (x + width + border > metadata.TextureWidth)
            {
                newLine = true;
                y += height + border;
                x = border;
            }

            if (newLine)
            {
                textureHeight += height + border;
                newLine = false;
            }

            glyphs[i] = glyph;
            rects[i] = new Rect(x, y, width, height);
            x += width + border;
        }

        //
        // Draw.
        //

        var size = new Size(metadata.TextureWidth, textureHeight);
        var bitmapSize = PixelSize.FromSizeWithDpi(size, RenderDpi);
        var imageInfo = new SKImageInfo(bitmapSize.Width, bitmapSize.Height);

        using var glyphRender = RenderGlyphs(bitmapSize, padding, rects, glyphs, metadata);
        using var glyphBitmap = ToSKBitmap(glyphRender);

        var bitmap = new WriteableBitmap(bitmapSize, glyphRender.Dpi);
        using var bitmapBuffer = bitmap.Lock();

        // Background.
        using var surface = SKSurface.Create(imageInfo, bitmapBuffer.Address, bitmapBuffer.RowBytes);
        surface.Canvas.Clear(SKColors.Fuchsia);

        using var backgroundPaint = new SKPaint();
        backgroundPaint.Color = SKColors.Transparent;
        backgroundPaint.Style = SKPaintStyle.Fill;
        backgroundPaint.BlendMode = SKBlendMode.Clear;

        for (var i = 0; i < glyphs.Length; i++)
        {
            surface.Canvas.DrawRect(rects[i].ToSKRect(), backgroundPaint);
        }

        // Glyphs.
        using var foregroundPaint = new SKPaint();
        foregroundPaint.BlendMode = SKBlendMode.DstOver;

        if (metadata.DropShadow != null)
        {
            foregroundPaint.ImageFilter = SKImageFilter.CreateDropShadow(
                metadata.DropShadow.OffsetX, metadata.DropShadow.OffsetY,
                metadata.DropShadow.Blur, metadata.DropShadow.Blur,
                new SKColor(metadata.DropShadow.Color));
        }
        
        surface.Canvas.DrawBitmap(glyphBitmap, 0, 0, foregroundPaint);
        surface.Flush();

        return bitmap;
    }

    private async void OnImageSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
        {
            return;
        }

        var bitmap = Preview.Source;
        if (bitmap == null)
        {
            return;
        }

        // Adjust image size to match device pixels.
        var bitmapSize = bitmap.Size;
        var width = bitmapSize.Width / topLevel.RenderScaling;

        TextureHeightTextBox.Text = $"{Math.Ceiling(bitmapSize.Height)}";

        if (double.IsNaN(Preview.Width) || Math.Abs(Preview.Width - width) > LayoutHelper.LayoutEpsilon)
        {
            Preview.Width = width;

            // Wait for layout pass.
            await Task.Yield();

            ScrollArea.Offset = new Vector(ScrollArea.ScrollBarMaximum.X / 2, ScrollArea.Offset.Y);
        }
    }

    private static RenderTargetBitmap RenderGlyphs(PixelSize bitmapSize, Thickness padding, Rect[] rects,
        FormattedText[] glyphs, TextureMetadata metadata)
    {
        var bitmap = new RenderTargetBitmap(bitmapSize, new Vector(RenderDpi, RenderDpi));
        using var canvas = bitmap.CreateDrawingContext();

        SetRenderOptions(canvas,
                         options => options with
                         {
                             EdgeMode = metadata.Antialiased
                                 ? EdgeMode.Antialias
                                 : EdgeMode.Aliased,
                             TextRenderingMode = metadata.Antialiased
                                 ? TextRenderingMode.Antialias
                                 : TextRenderingMode.Alias,
                         });

        if (metadata.Outline != null)
        {
            // Geometry can only be processed on UI thread.
            Dispatcher.UIThread.Invoke([SuppressMessage("ReSharper", "AccessToDisposedClosure")] () =>
            {
                var foreground = new ImmutableSolidColorBrush(metadata.Foreground);
                var stroke = new Pen(metadata.Outline.Color, metadata.Outline.Width);

                for (var i = 0; i < rects.Length; i++)
                {
                    var rect = rects[i];
                    using var clip = canvas.PushClip(rect);

                    var glyph = glyphs[i];
                    var offset = (glyph.OverhangTrailing + glyph.OverhangLeading) / 2;

                    var geometry = glyph.BuildGeometry(new Point(rect.X + padding.Left + offset, rect.Y + padding.Top))!;
                    canvas.DrawGeometry(foreground, stroke, geometry);
                }
            });
        }
        else
        {
            for (var i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                using var clip = canvas.PushClip(rect);

                var glyph = glyphs[i];
                var offset = (glyph.OverhangTrailing + glyph.OverhangLeading) / 2;

                canvas.DrawText(glyph, new Point(rect.X + padding.Left + offset, rect.Y + padding.Top));
            }
        }

        return bitmap;
    }

    private static void SetRenderOptions(DrawingContext canvas, Func<RenderOptions, RenderOptions> action)
    {
        // WORKAROUND: There's currently no public API to specify custom RenderOptions for DrawingContext.
        // https://github.com/AvaloniaUI/Avalonia/issues/2464
        var propertyInfo = canvas.GetType().GetProperty("RenderOptions")!;
        var renderOptions = (RenderOptions)propertyInfo.GetValue(canvas)!;
        propertyInfo.SetValue(canvas, action(renderOptions));
    }

    private static SKBitmap ToSKBitmap(Bitmap bitmap)
    {
        var result = new SKBitmap(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), result.GetPixels(), result.ByteCount, result.RowBytes);
        return result;
    }
}
