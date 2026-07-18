using Microsoft.Xna.Framework.Content.Pipeline;
using MonoGame.Framework.Content.Pipeline.Builder;
using ShipGame.Content;

var repository = new DirectoryInfo(AppContext.BaseDirectory);
while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ShipGame.sln")))
    repository = repository.Parent;
var repositoryRoot = repository?.FullName
    ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
var sourceRoot = Path.Combine(repositoryRoot, "content", "source");

AssetManifest manifest;
try
{
    manifest = ContentValidator.LoadAndValidateManifest(sourceRoot, "data/asset-manifest.json");
}
catch (ContentValidationException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

var builder = new ShipContentBuilder(manifest);
builder.Run(new ContentBuilderParams
{
    Mode = ContentBuilderMode.Builder,
    WorkingDirectory = repositoryRoot,
    SourceDirectory = Path.Combine("content", "source"),
    OutputDirectory = Path.Combine("content", "generated", "DesktopVK"),
    IntermediateDirectory = Path.Combine("tools", "ShipGame.ContentBuilder", "obj", "Content"),
    Platform = TargetPlatform.DesktopVK,
    CompressContent = false,
    Rebuild = args.Contains("--rebuild", StringComparer.Ordinal)
});

return builder.FailedToBuild > 0 ? 1 : 0;

public sealed class ShipContentBuilder(AssetManifest manifest) : ContentBuilder
{
    public override IContentCollection GetContentCollection()
    {
        var content = new ContentCollection();
        foreach (var source in ContentBuildPlan.DataSources(manifest))
            content.IncludeCopy<WildcardRule>(source);
        return content;
    }
}
