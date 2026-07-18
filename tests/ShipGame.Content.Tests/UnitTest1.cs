using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content.Tests;

public class ContentTests
{
    [Fact]
    public void ManifestRejectsDuplicateIds()
    {
        using var fixture = new ManifestFixture(
            new AssetEntry("data/a", "data", "a.json", "placeholder", "test", "CC0-1.0"),
            new AssetEntry("data/a", "data", "b.json", "placeholder", "test", "CC0-1.0"));

        var exception = Assert.Throws<ContentValidationException>(() => fixture.Validate());
        Assert.Contains(exception.Issues, issue => issue.Code == "asset.duplicate-id");
    }

    [Fact]
    public void ManifestRejectsMissingSource()
    {
        using var fixture = new ManifestFixture(
            new AssetEntry("data/missing", "data", "missing.json", "placeholder", "test", "CC0-1.0"));

        var exception = Assert.Throws<ContentValidationException>(() => fixture.Validate());
        Assert.Contains(exception.Issues, issue => issue.Code == "asset.missing-source");
    }

    [Fact]
    public void DefinitionsRejectMissingReferences()
    {
        var exception = Assert.Throws<ContentValidationException>(() =>
            ContentValidator.ValidateDefinitions(
                [new(new ContentId("A"), [new ContentId("B")])]));

        Assert.Contains(exception.Issues, issue => issue.Code == "content.missing-reference");
    }

    [Fact]
    public void PathsCannotEscapeSourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => ContentValidator.ResolveUnderRoot(root, "../escape.json"));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"assets\":null}")]
    [InlineData("{\"schemaVersion\":1}")]
    public void ManifestRequiresAssetsArray(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "ShipGame-Content-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "manifest.json"), json);
            var exception = Assert.Throws<ContentValidationException>(() =>
                ContentValidator.LoadAndValidateManifest(root, "manifest.json"));
            Assert.Contains(exception.Issues, issue => issue.Code == "manifest.assets-required");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DefinitionsExposeTypedValidatedIds()
    {
        var definition = new ContentDefinition(new ContentId("module/a"), [new ContentId("module/b")]);
        ContentValidator.ValidateDefinitions(
            [definition, new ContentDefinition(new ContentId("module/b"))]);

        Assert.Equal(new ContentId("module/a"), definition.Id);
        Assert.Throws<ArgumentException>(() => new ContentId(""));
        Assert.Throws<ArgumentException>(() => new ContentId(" "));
    }

    [Fact]
    public void ManifestDataAdditionsEnterBuildPlanWithoutBuilderEdits()
    {
        var manifest = new AssetManifest(
            ContractVersions.Content,
            [
                new("data/z", "data", "data/z.json", "placeholder", "test", "CC0-1.0"),
                new("data/a", "data", "data/a.json", "placeholder", "test", "CC0-1.0")
            ]);

        Assert.Equal(
            ["data/asset-manifest.json", "data/a.json", "data/z.json"],
            ContentBuildPlan.DataSources(manifest));
    }

    private sealed class ManifestFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ShipGame-Content-" + Guid.NewGuid().ToString("N"));

        public ManifestFixture(params AssetEntry[] entries)
        {
            Directory.CreateDirectory(_root);
            foreach (var entry in entries)
                if (!entry.Source.Contains("missing", StringComparison.Ordinal))
                    File.WriteAllText(Path.Combine(_root, entry.Source), "{}");
            File.WriteAllText(
                Path.Combine(_root, "manifest.json"),
                JsonSerializer.Serialize(new AssetManifest(1, entries)));
        }

        public AssetManifest Validate() => ContentValidator.LoadAndValidateManifest(_root, "manifest.json");

        public void Dispose() => Directory.Delete(_root, true);
    }
}
