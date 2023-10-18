using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
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
    private static readonly Vector RenderDpi = new(96.0, 96.0);

    public MainView()
    {
        InitializeComponent();
    }

    public void Show(string message)
    {
        ErrorPopup.DataContext = message;
        ErrorPopup.IsOpen = true;
    }

    public Bitmap Render(IReadOnlyList<string> chars, TextureMetadata metadata, out Glyph[] glyphs,
        CancellationToken cancellationToken = default)
    {
        //
        // Measure and arrange.
        //

        var outline = GetPen(metadata.Outline);
        var fontBrush = new ImmutableSolidColorBrush(metadata.Foreground);
        var alphaMaskMode = outline == null && metadata is { Foreground: > 0, Antialiased: true };

        IBrush? background;
        IBrush foreground;

        if (alphaMaskMode)
        {
            // WORKAROUND: When using TextRenderingMode.SubpixelAntialias, Avalonia produces artifacts
            // when drawing FormattedText on RenderTargetBitmap with transparent background.
            // To avoid this issue, draw the text in black on white, and then convert it to alpha mask,
            // which is then used to compose the final image by blending with foreground color.
            background = Brushes.White;
            foreground = Brushes.Black;
        }
        else
        {
            background = null;
            foreground = fontBrush;
        }

        (glyphs, var glyphKinds, var size) = Arrange(chars, metadata, foreground, cancellationToken);

        //
        // Draw.
        //

        using var glyphBitmap = Render(size, glyphs, GlyphKinds.Regular, background, foreground,
                                       outline, metadata, true, cancellationToken);

        var bitmap = new WriteableBitmap(size, RenderDpi);
        using var bitmapBuffer = bitmap.Lock();

        // Background.
        var imageInfo = new SKImageInfo(size.Width, size.Height);
        using var surface = SKSurface.Create(imageInfo, bitmapBuffer.Address, bitmapBuffer.RowBytes);
        surface.Canvas.Clear(SKColors.Fuchsia);

        using var backgroundPaint = new SKPaint();
        backgroundPaint.Color = SKColors.Transparent;
        backgroundPaint.BlendMode = SKBlendMode.Clear;

        foreach (var glyph in glyphs.AsSpan())
        {
            surface.Canvas.DrawRect(glyph.Rect.ToSKRect(), backgroundPaint);
        }

        // Glyphs.
        using var glyphPaint = new SKPaint();
        glyphPaint.BlendMode = SKBlendMode.DstOver;

        if (alphaMaskMode)
        {
            using var tintFilter = SKColorFilter.CreateBlendMode(metadata.Foreground, SKBlendMode.SrcOut);
            using var maskFilter = SKColorFilter.CreateLumaColor();
            using var colorFilter = SKColorFilter.CreateCompose(tintFilter, maskFilter);
            glyphPaint.ColorFilter = colorFilter;
        }

        if (metadata.DropShadow != null)
        {
            var blurSigma = SKMaskFilter.ConvertRadiusToSigma(metadata.DropShadow.Blur);

            using var dropShadow = SKImageFilter.CreateDropShadow(
                metadata.DropShadow.OffsetX, metadata.DropShadow.OffsetY,
                blurSigma, blurSigma,
                metadata.DropShadow.Color);

            glyphPaint.ImageFilter = dropShadow;
        }

        surface.Canvas.DrawBitmap(glyphBitmap, 0, 0, glyphPaint);

        // Emojis.
        if (glyphKinds.HasFlag(GlyphKinds.Emoji))
        {
            using var emojiPaint = new SKPaint();
            emojiPaint.ImageFilter = glyphPaint.ImageFilter;
            emojiPaint.BlendMode = SKBlendMode.DstOver;

            using var emojiBitmap = Render(size, glyphs, GlyphKinds.Emoji, null, fontBrush,
                                           outline, metadata, false, CancellationToken.None /* Too late to cancel */);

            surface.Canvas.DrawBitmap(emojiBitmap, 0, 0, emojiPaint);
        }

        surface.Flush();

        return bitmap;
    }

    private static (Glyph[], GlyphKinds, PixelSize) Arrange(IReadOnlyList<string> chars, TextureMetadata metadata,
        IBrush foreground, CancellationToken cancellationToken)
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

        var typeface = new Typeface(metadata.FontName, metadata.FontStyle, metadata.FontWeight, metadata.FontStretch);
        var outline = GetPen(metadata.Outline);
        var culture = CultureInfo.CurrentCulture;

        var x = border;
        var y = border;
        var textureHeight = border;
        var newLine = true;

        var glyphKinds = GlyphKinds.None;
        var glyphs = new Glyph[chars.Count];

        for (var i = 0; i < chars.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chr = chars[i];

            // Replace control characters with placeholder.
            if (chr.Length == 1 && char.IsControl(chr, 0))
            {
                chr = CharHelper.Placeholder;
            }
            
            var text = new FormattedText(chr, culture, flow, typeface, metadata.FontSize, foreground);
            var height = (int)Math.Ceiling(text.Height);
            var geometry = GetGeometry(text, outline, out var glyphBounds);
            var width = (int)Math.Ceiling(glyphBounds.Width);
            var paddingRight = padding.Right;

            if (width == 0)
            {
                width = Math.Max(minWidth, (int)Math.Ceiling(text.WidthIncludingTrailingWhitespace));
            }
            else if (metadata.Kerning == TextureMetadata.AutomaticKerning)
            {
                var kerning = text.WidthIncludingTrailingWhitespace - glyphBounds.Width;
                if (kerning > paddingRight)
                {
                    paddingRight = kerning;
                }
            }

            if (glyphBounds.Width > 0 || text.WidthIncludingTrailingWhitespace > 0)
            {
                width += (int)Math.Ceiling(padding.Left + paddingRight);
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

            var glyph = new Glyph
            {
                Text = text,
                Geometry = geometry,
                Rect = new Rect(x, y, width, height),
                Offset = new Point(padding.Left - glyphBounds.Left, padding.Top),
                Kind = CharHelper.GetKind(chr),
            };

            x += width + border;
            glyphKinds |= glyph.Kind;
            glyphs[i] = glyph;
        }

        return (glyphs, glyphKinds, new PixelSize(metadata.TextureWidth, textureHeight));
    }

    private static SKBitmap Render(PixelSize size, Glyph[] glyphs, GlyphKinds kinds, IBrush? background, 
        IBrush foreground, IPen? outline, TextureMetadata metadata, bool subpixel,
        CancellationToken cancellationToken)
    {
        using var bitmap = new RenderTargetBitmap(size, RenderDpi);
        using var canvas = bitmap.CreateDrawingContext();

        if (background != null)
        {
            canvas.FillRectangle(background, new Rect(bitmap.Size));
        }

        canvas.PushRenderOptions(new RenderOptions
        {
            EdgeMode = metadata.Antialiased
                ? EdgeMode.Antialias
                : EdgeMode.Aliased,
            TextRenderingMode = metadata.Antialiased
                ? subpixel ? TextRenderingMode.SubpixelAntialias : TextRenderingMode.Antialias
                : TextRenderingMode.Alias,
        });

        if (outline != null)
        {
            // Geometry with Pen can only be rendered on UI thread.
            Dispatcher.UIThread.Invoke(Draw, DispatcherPriority.Send, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        else
        {
            Draw();
        }

        return bitmap.ToSKBitmap();

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        void Draw()
        {
            foreach (var glyph in glyphs.AsSpan())
            {
                if (!kinds.HasFlag(glyph.Kind))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var clip = canvas.PushClip(glyph.Rect);

                using var translate = canvas.PushTransform(
                    Matrix.CreateTranslation(glyph.Rect.X + glyph.Offset.X,
                                             glyph.Rect.Y + glyph.Offset.Y));

                if (outline != null)
                {
                    canvas.DrawGeometry(foreground, outline, glyph.Geometry);
                }
                else
                {
                    glyph.Text.SetForegroundBrush(foreground);
                    canvas.DrawText(glyph.Text, default);
                }
            }
        }
    }

    private static Geometry GetGeometry(FormattedText text, IPen? outline, out Rect bounds)
    {
        (var geometry, bounds) = Dispatcher.UIThread.Invoke(() =>
        {
            // Geometry can only be built on UI thread.
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
        
        UpdateScaleMargin();
    }

    private void OnRenderScaleChanged(object? sender, EventArgs e)
    {
        UpdateScaleMargin();
    }

    private void UpdateScaleMargin()
    {
        var scaleTransform = (ScaleTransform)Preview.RenderTransform!;
        var size = Preview.Bounds.Size;

        Preview.Margin = new Thickness(size.Width / 2 * (scaleTransform.ScaleX - 1) - 2,
                                       size.Height / 2 * (scaleTransform.ScaleY - 1) - 2);
    }
}
