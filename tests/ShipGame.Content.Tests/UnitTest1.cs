using System.Text.Json;

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
            ContentValidator.ValidateDefinitions([new("A", ["B"])]));

        Assert.Contains(exception.Issues, issue => issue.Code == "content.missing-reference");
    }

    [Fact]
    public void PathsCannotEscapeSourceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => ContentValidator.ResolveUnderRoot(root, "../escape.json"));
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
