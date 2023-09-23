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

    public Bitmap Render(IReadOnlyList<string> chars, TextureMetadata metadata, out Glyph[] glyphs)
    {
        const int border = 3;
        const int minWidth = 1;
        const FlowDirection flow = FlowDirection.LeftToRight;

        var shadowBlur = metadata.DropShadow?.Blur ?? 0;
        var shadowOffsetX = metadata.DropShadow?.OffsetX ?? 0;
        var shadowOffsetY = metadata.DropShadow?.OffsetY ?? 0;

        var padding = new Thickness(Math.Max(0, shadowBlur - shadowOffsetX),
                                    Math.Max(0, shadowBlur - shadowOffsetY),
                                    Math.Max(shadowBlur + shadowOffsetX, metadata.Kerning),
                                    Math.Max(shadowBlur + shadowOffsetY, metadata.Leading));
        //
        // Measure and arrange.
        //

        var foreground = new ImmutableSolidColorBrush(metadata.Foreground);
        var typeface = new Typeface(metadata.FontName, metadata.FontStyle, metadata.FontWeight, metadata.FontStretch);
        var outline = GetPen(metadata.Outline);
        var culture = CultureInfo.CurrentCulture;

        var x = border;
        var y = border;
        var textureHeight = border;
        var newLine = true;

        glyphs = new Glyph[chars.Count];

        for (var i = 0; i < chars.Count; i++)
        {
            var chr = chars[i];

            // Replace control characters with placeholder.
            if (chr.Length == 1 && char.IsControl(chr, 0))
            {
                chr = "\u25A1";
            }

            var text = new FormattedText(chr, culture, flow, typeface, metadata.FontSize, foreground);
            var height = (int)Math.Ceiling(text.Height);
            var glyph = GetGeometry(text, outline, out var glyphBounds);
            var width = (int)Math.Ceiling(glyphBounds.Width);

            if (width == 0)
            {
                // Special case for a whitespace character.
                width = Math.Max(minWidth, (int)Math.Ceiling(text.WidthIncludingTrailingWhitespace));
            }
            else
            {
                width += (int)Math.Ceiling(padding.Left + padding.Right);
            }

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

            glyphs[i] = new Glyph
            {
                Geometry = glyph,
                Rect = new Rect(x, y, width, height),
                Offset = new Point(padding.Left - glyphBounds.Left, padding.Top),
            };

            x += width + border;
        }

        //
        // Draw.
        //

        var size = new Size(metadata.TextureWidth, textureHeight);
        var bitmapSize = PixelSize.FromSizeWithDpi(size, RenderDpi);
        var imageInfo = new SKImageInfo(bitmapSize.Width, bitmapSize.Height);

        using var glyphRender = RenderGlyphs(bitmapSize, glyphs, outline, metadata);
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

        foreach (var glyph in glyphs)
        {
            surface.Canvas.DrawRect(glyph.Rect.ToSKRect(), backgroundPaint);
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

    private static Bitmap RenderGlyphs(PixelSize size, Glyph[] glyphs, IPen? outline, TextureMetadata metadata)
    {
        var bitmap = new RenderTargetBitmap(size, new Vector(RenderDpi, RenderDpi));
        using var canvas = bitmap.CreateDrawingContext();

        SetRenderOptions(canvas,
                         options => options with
                         {
                             EdgeMode = metadata.Antialiased
                                 ? EdgeMode.Antialias
                                 : EdgeMode.Aliased,
                         });

        var foreground = new ImmutableSolidColorBrush(metadata.Foreground);

        if (metadata.Outline != null)
        {
            // Geometry with Pen can only be rendered on UI thread.
            Dispatcher.UIThread.Invoke(Render);
        }
        else
        {
            Render();
        }

        return bitmap;

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        void Render()
        {
            foreach (var glyph in glyphs)
            {
                using var clip = canvas.PushClip(glyph.Rect);

                using var translate = canvas.PushTransform(
                    Matrix.CreateTranslation(glyph.Rect.X + glyph.Offset.X,
                                             glyph.Rect.Y + glyph.Offset.Y));

                canvas.DrawGeometry(foreground, outline, glyph.Geometry);
            }
        }
    }

    private static Geometry GetGeometry(FormattedText text, IPen? outline, out Rect bounds)
    {
        (var geometry, bounds) = Dispatcher.UIThread.Invoke(() =>
        {
            // Geometry can only be built on the UI thread.
            var geometry = text.BuildGeometry(default)!;
            var bounds = outline == null ? geometry.Bounds : geometry.GetRenderBounds(outline);
            return (geometry, bounds);
        });

        return geometry;
    }

    private static Pen? GetPen(StrokeMetadata? outline)
    {
        // Pen can only be created on UI thread.
        return outline != null
            ? Dispatcher.UIThread.Invoke(() => new Pen(outline.Color, outline.Width))
            : null;
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
