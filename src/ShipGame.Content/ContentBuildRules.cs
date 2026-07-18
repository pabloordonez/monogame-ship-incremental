namespace ShipGame.Content;

/// <summary>
/// P1-owned content build rules for texture/atlas/sound/data/metadata kinds.
/// Extends the P0 data-only <see cref="ContentBuildPlan"/> without legacy MGCB.
/// </summary>
public static class ContentBuildRules
{
    public const string TextureProcessorVersion = "p1-texture-v1";
    public const string SoundProcessorVersion = "p1-sound-v1";

    public sealed record BuildItem(
        string AssetId,
        string Kind,
        string SourceRelativePath,
        string OutputRelativePath,
        string ProcessorKey);

    public static IReadOnlyList<BuildItem> Enumerate(AssetManifestV1 manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Assets is null)
            throw new ContentValidationException([new("manifest.assets-required", "Manifest assets must be an array.")]);

        var items = new List<BuildItem>();
        foreach (var asset in manifest.Assets.OrderBy(asset => asset.Id, StringComparer.Ordinal))
        {
            var source = asset.Source.Replace('\\', '/');
            var extension = Path.GetExtension(source);
            switch (asset.Kind)
            {
                case "data":
                case "metadata":
                    items.Add(new(asset.Id, asset.Kind, source, asset.Id + extension, "copy"));
                    break;
                case "atlas":
                case "texture":
                    items.Add(new(asset.Id, asset.Kind, source, asset.Id, TextureProcessorVersion));
                    break;
                case "sound":
                    items.Add(new(asset.Id, asset.Kind, source, asset.Id, SoundProcessorVersion));
                    break;
                default:
                    throw new ContentValidationException(
                        [new("asset.unsupported-build-kind", $"No reviewed P1 build rule for kind '{asset.Kind}' ({asset.Id}).")]);
            }
        }

        return items;
    }

    public static TextureBuildOptions TextureOptions { get; } = new(
        GenerateMipmaps: false,
        ResizeToPowerOfTwo: false,
        MakeSquare: false,
        ColorKeyEnabled: false,
        PremultiplyAlpha: true,
        SamplerHint: "PointClamp");

    public sealed record TextureBuildOptions(
        bool GenerateMipmaps,
        bool ResizeToPowerOfTwo,
        bool MakeSquare,
        bool ColorKeyEnabled,
        bool PremultiplyAlpha,
        string SamplerHint);
}
