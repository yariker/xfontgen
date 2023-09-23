namespace XnaFontTextureGenerator.Model;

public record DropShadowMetadata
{
    public const int DefaultShadowBlur = 3;
    public const int DefaultShadowOffsetX = 1;
    public const int DefaultShadowOffsetY = 1;
    public const uint DefaultShadowColor = 0xFF000000;

    public float Blur { get; set; }

    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

    public uint Color { get; set; }
}