namespace XnaFontTextureGenerator.Model;

public record DropShadowMetadata
{
    public float Blur { get; set; }

    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

    public uint Color { get; set; }
}