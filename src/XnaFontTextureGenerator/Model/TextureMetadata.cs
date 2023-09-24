using System.Text.Json;
using Avalonia.Media;

namespace XnaFontTextureGenerator.Model;

public record TextureMetadata
{
    public const string Creator = "XNA Font Texture Generator";
    public const string Keyword = "JSON:xftgen.metadata";

    public const float DefaultFontSize = 30;
    public const uint DefaultForegroundColor = 0xFFFFFFFF;

    public const int AutomaticKerning = -1;

    public string FontName { get; set; } = null!;

    public FontWeight FontWeight { get; set; }

    public FontStretch FontStretch { get; set; }

    public FontStyle FontStyle { get; set; }

    public float FontSize { get; set; }

    public int Kerning { get; set; }

    public int Leading { get; set; }

    public int MinChar { get; set; }

    public int MaxChar { get; set; }

    public bool Antialiased { get; set; }

    public uint Foreground { get; set; }

    public int TextureWidth { get; set; }

    public DropShadowMetadata? DropShadow { get; set; }

    public StrokeMetadata? Outline { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this);

    public static TextureMetadata? FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TextureMetadata>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}