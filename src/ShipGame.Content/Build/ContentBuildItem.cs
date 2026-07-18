namespace ShipGame.Content;

public sealed record ContentBuildItem(
    string AssetId,
    string Kind,
    string SourceRelativePath,
    string OutputRelativePath,
    string ProcessorKey);
