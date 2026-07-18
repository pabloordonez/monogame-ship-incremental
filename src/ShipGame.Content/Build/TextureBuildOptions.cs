namespace ShipGame.Content;

public sealed record TextureBuildOptions(
    bool GenerateMipmaps,
    bool ResizeToPowerOfTwo,
    bool MakeSquare,
    bool ColorKeyEnabled,
    bool PremultiplyAlpha,
    string SamplerHint);
