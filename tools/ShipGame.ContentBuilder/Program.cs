using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using MonoGame.Framework.Content.Pipeline.Builder;
using ShipGame.Content;
using System.Runtime.InteropServices;
using System.Text.Json;

EnsureMonoGameAudioToolsOnPath();

var repository = new DirectoryInfo(AppContext.BaseDirectory);
while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "ShipGame.sln")))
    repository = repository.Parent;
var repositoryRoot = repository?.FullName
    ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
var sourceRoot = Path.Combine(repositoryRoot, "content", "source");
var definitionsRoot = Path.Combine(repositoryRoot, "content", "definitions");

if (args.Contains("--pack-atlases", StringComparer.Ordinal)
    || args.Contains("--generate-source", StringComparer.Ordinal))
{
    SourceAssetGenerator.PackAtlases(sourceRoot);
    return 0;
}

AssetManifestV1 manifest;
try
{
    var catalog = MvpContentLoader.LoadAndValidate(sourceRoot, definitionsRoot);
    manifest = JsonSerializer.Deserialize<AssetManifestV1>(
        File.ReadAllText(Path.Combine(sourceRoot, "data", "asset-manifest.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Manifest is empty.");
    _ = ContentBuildRules.Enumerate(manifest);
    Console.WriteLine($"Validated catalog {catalog.CatalogVersion} ({catalog.Fingerprint}).");
}
catch (Exception exception) when (exception is ContentValidationException or JsonException or IOException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

var builder = new ShipContentBuilder(manifest);
builder.Run(new ContentBuilderParams
{
    Mode = ContentBuilderMode.Builder,
    WorkingDirectory = repositoryRoot,
    SourceDirectory = ".",
    OutputDirectory = Path.Combine("content", "generated", "DesktopVK"),
    IntermediateDirectory = Path.Combine("tools", "ShipGame.ContentBuilder", "obj", "Content"),
    Platform = TargetPlatform.DesktopVK,
    CompressContent = false,
    Rebuild = args.Contains("--rebuild", StringComparer.Ordinal)
});

if (builder.FailedToBuild > 0)
    return 1;

// Retain authored sources beside compiled outputs so P0 fail-closed
// ContentValidator.LoadAndValidateManifest can resolve Source paths without
// softening ContentContracts (IncludeCopy of the same input replaces Include).
var generatedContent = Path.Combine(repositoryRoot, "content", "generated", "DesktopVK", "Content");
foreach (var item in ContentBuildRules.Enumerate(manifest))
{
    if (item.ProcessorKey is not (ContentBuildRules.TextureProcessorVersion or ContentBuildRules.SoundProcessorVersion))
        continue;
    var from = Path.Combine(repositoryRoot, "content", "source", item.SourceRelativePath);
    var to = Path.Combine(generatedContent, item.SourceRelativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
    File.Copy(from, to, overwrite: true);
}

return 0;

static void EnsureMonoGameAudioToolsOnPath()
{
    var ridFolder = OperatingSystem.IsWindows()
        ? RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "windows-arm64" : "windows-x64"
        : OperatingSystem.IsMacOS()
            ? "osx"
            : RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";

    var nugetRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
    if (string.IsNullOrWhiteSpace(nugetRoot))
        nugetRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages");

    var directories = new List<string>();
    foreach (var package in new[] { "monogame.tool.ffprobe", "monogame.tool.ffmpeg" })
    {
        var packageRoot = Path.Combine(nugetRoot, package);
        if (!Directory.Exists(packageRoot))
            continue;
        foreach (var versionDir in Directory.GetDirectories(packageRoot).OrderByDescending(Path.GetFileName, StringComparer.Ordinal))
        {
            var binaries = Path.Combine(versionDir, "binaries", ridFolder);
            if (Directory.Exists(binaries))
            {
                directories.Add(binaries);
                break;
            }
        }
    }

    var outputCandidate = Path.Combine(AppContext.BaseDirectory, ridFolder);
    if (Directory.Exists(outputCandidate))
        directories.Insert(0, outputCandidate);

    if (directories.Count == 0)
        return;

    var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    Environment.SetEnvironmentVariable(
        "PATH",
        string.Join(Path.PathSeparator, directories.Append(current)));
}

public sealed class ShipContentBuilder(AssetManifestV1 manifest) : ContentBuilder
{
    public override IContentCollection GetContentCollection()
    {
        var content = new ContentCollection();
        content.SetContentRoot("Content");
        content.IncludeCopy("content/source/data/asset-manifest.json", "data/asset-manifest.json");
        content.IncludeCopy("content/definitions/mvp-catalog.json", "data/mvp-catalog.json");

        var textureOptions = ContentBuildRules.TextureOptions;
        foreach (var item in ContentBuildRules.Enumerate(manifest))
        {
            var input = "content/source/" + item.SourceRelativePath;
            switch (item.ProcessorKey)
            {
                case "copy":
                    content.IncludeCopy(input, item.OutputRelativePath);
                    break;
                case ContentBuildRules.TextureProcessorVersion:
                    content.Include(
                        input,
                        item.OutputRelativePath,
                        new TextureImporter(),
                        new TextureProcessor
                        {
                            ColorKeyEnabled = textureOptions.ColorKeyEnabled,
                            GenerateMipmaps = textureOptions.GenerateMipmaps,
                            PremultiplyAlpha = textureOptions.PremultiplyAlpha,
                            ResizeToPowerOfTwo = textureOptions.ResizeToPowerOfTwo,
                            MakeSquare = textureOptions.MakeSquare,
                            TextureFormat = TextureProcessorOutputFormat.Color
                        });
                    break;
                case ContentBuildRules.SoundProcessorVersion:
                    content.Include(input, item.OutputRelativePath, new WavImporter(), new SoundEffectProcessor());
                    break;
                default:
                    throw new ContentValidationException(
                        [new("asset.unsupported-build-kind", $"Unknown processor '{item.ProcessorKey}' for '{item.AssetId}'.")]);
            }
        }

        return content;
    }
}
