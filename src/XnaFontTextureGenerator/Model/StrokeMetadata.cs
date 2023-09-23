namespace XnaFontTextureGenerator.Model;

public record StrokeMetadata
{
    public const float DefaultOutlineWidth = 1;
    public const uint DefaultOutlineColor = 0xFFFF0000;

    public float Width { get; set; }

    public uint Color { get; set; }
}