using ShipGame.Domain;

namespace ShipGame.Content.Tests;

public class MvpContentTests
{
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ShipGame.sln")))
                directory = directory.Parent;
            return directory?.FullName
                ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
        }
    }

    private static string SourceRoot => Path.Combine(RepositoryRoot, "content", "source");
    private static string DefinitionsRoot => Path.Combine(RepositoryRoot, "content", "definitions");

    [Fact]
    public void MvpCatalogLoadsWithUniqueIdsAndValidResearchGraph()
    {
        var catalog = MvpContentLoader.LoadAndValidate(SourceRoot, DefinitionsRoot);

        Assert.Equal("mvp-p1-v1", catalog.CatalogVersion);
        Assert.False(string.IsNullOrWhiteSpace(catalog.Fingerprint));
        Assert.Contains("MAT_FERRITE", catalog.DefinitionIds);
        Assert.Contains("RES_NAV_ION_VEIL", catalog.DefinitionIds);
        Assert.Contains("atlases/player-modules", catalog.AssetIds);
        Assert.Contains("ships/player/wayfarer", catalog.RegionIds);

        var wayfarer = catalog.GetRegion("ships/player/wayfarer");
        Assert.Equal(64, wayfarer.Width);
        Assert.Equal(64, wayfarer.Height);
        Assert.Equal(0.5, wayfarer.PivotX);
        Assert.Equal(0.5, wayfarer.PivotY);
        Assert.NotNull(wayfarer.Hardpoints);
        Assert.Contains("primaryWeapon", wayfarer.Hardpoints.Keys);
        Assert.Contains("shieldOrigin", wayfarer.Hardpoints.Keys);

        Assert.Equal(32, catalog.GetRegion("asteroids/small/ordinary").Width);
        Assert.Equal(64, catalog.GetRegion("asteroids/medium/ordinary").Width);
        Assert.Equal(96, catalog.GetRegion("asteroids/large/ordinary").Width);
        Assert.InRange(catalog.GetRegion("pickups/ferrite").Width, 8, 12);
        Assert.Equal(32, catalog.GetRegion("ui/icons/hull").Width);
    }

    [Fact]
    public void ManifestCandidatesHaveProvenanceLicenseAndReplacementCriteria()
    {
        var catalog = MvpContentLoader.LoadAndValidate(SourceRoot, DefinitionsRoot);
        foreach (var id in catalog.AssetIds)
        {
            var asset = catalog.GetAsset(id);
            Assert.False(string.IsNullOrWhiteSpace(asset.License));
            Assert.False(string.IsNullOrWhiteSpace(asset.Provenance));
            Assert.True(asset.Owner is "P1_CONTENT_ART" or "P0_FOUNDATION");
            Assert.True(asset.License is "CC0-1.0" or "proprietary");
            if (asset.Status is "candidate" or "placeholder")
            {
                Assert.False(string.IsNullOrWhiteSpace(asset.ReplacementCriterion));
                Assert.False(string.IsNullOrWhiteSpace(asset.Waiver));
            }
        }
    }

    [Fact]
    public void ContentBuildRulesCoverTextureAtlasSoundWithoutMgcb()
    {
        var manifest = System.Text.Json.JsonSerializer.Deserialize<AssetManifestV1>(
            File.ReadAllText(Path.Combine(SourceRoot, "data", "asset-manifest.json")),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifest missing.");

        var items = ContentBuildRules.Enumerate(manifest);
        Assert.Contains(items, item => item.Kind == "atlas" && item.ProcessorKey == ContentBuildRules.TextureProcessorVersion);
        Assert.Contains(items, item => item.Kind == "texture" && item.ProcessorKey == ContentBuildRules.TextureProcessorVersion);
        Assert.Contains(items, item => item.Kind == "sound" && item.ProcessorKey == ContentBuildRules.SoundProcessorVersion);
        Assert.Contains(items, item => item.Kind is "data" or "metadata" && item.ProcessorKey == "copy");
        Assert.False(ContentBuildRules.TextureOptions.GenerateMipmaps);
        Assert.False(ContentBuildRules.TextureOptions.ResizeToPowerOfTwo);
        Assert.Equal("PointClamp", ContentBuildRules.TextureOptions.SamplerHint);
        Assert.DoesNotContain(Directory.EnumerateFiles(RepositoryRoot, "*.mgcb", SearchOption.AllDirectories), _ => true);
    }

    [Fact]
    public void DataSourcesRemainCompatibleForDataOnlySubset()
    {
        var manifest = new AssetManifest(
            ContractVersions.Content,
            [
                new("data/z", "data", "data/z.json", "placeholder", "test", "CC0-1.0"),
                new("atlases/a", "atlas", "textures/a.png", "candidate", "test", "CC0-1.0"),
                new("data/a", "data", "data/a.json", "placeholder", "test", "CC0-1.0")
            ]);

        Assert.Equal(
            ["data/asset-manifest.json", "data/a.json", "data/z.json"],
            ContentBuildPlan.DataSources(manifest));
    }

    [Fact]
    public void ResearchCycleIsRejected()
    {
        using var fixture = TemporaryCatalog.Create(
            """
            {
              "schemaVersion": 1,
              "catalogVersion": "cycle",
              "definitions": [
                { "id": "RES_A", "kind": "research", "references": ["RES_B"], "values": { "ferrite": 1, "lumen": 0, "dataCore": 0 } },
                { "id": "RES_B", "kind": "research", "references": ["RES_A"], "values": { "ferrite": 1, "lumen": 0, "dataCore": 0 } }
              ]
            }
            """);

        var exception = Assert.Throws<ContentValidationException>(() =>
            MvpContentLoader.LoadAndValidate(fixture.SourceRoot, fixture.DefinitionsRoot));
        Assert.Contains(exception.Issues, issue => issue.Code == "catalog.research-cycle");
    }

    [Fact]
    public void NegativeCatalogRangesAreRejected()
    {
        using var fixture = TemporaryCatalog.Create(
            """
            {
              "schemaVersion": 1,
              "catalogVersion": "bad-range",
              "definitions": [
                { "id": "MAT_FERRITE", "kind": "resource", "references": [], "values": { "yieldMin": -1, "yieldMax": 4 } }
              ]
            }
            """);

        var exception = Assert.Throws<ContentValidationException>(() =>
            MvpContentLoader.LoadAndValidate(fixture.SourceRoot, fixture.DefinitionsRoot));
        Assert.Contains(exception.Issues, issue => issue.Code == "catalog.range");
    }

    private sealed class TemporaryCatalog : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ShipGame-Mvp-" + Guid.NewGuid().ToString("N"));
        public string SourceRoot => Path.Combine(Root, "source");
        public string DefinitionsRoot => Path.Combine(Root, "definitions");

        public static TemporaryCatalog Create(string catalogJson)
        {
            var fixture = new TemporaryCatalog();
            Directory.CreateDirectory(Path.Combine(fixture.SourceRoot, "data"));
            Directory.CreateDirectory(fixture.DefinitionsRoot);
            File.WriteAllText(Path.Combine(fixture.DefinitionsRoot, "mvp-catalog.json"), catalogJson);
            File.WriteAllText(
                Path.Combine(fixture.SourceRoot, "data", "asset-manifest.json"),
                """
                {
                  "schemaVersion": 1,
                  "buildVersion": 1,
                  "assets": [
                    {
                      "id": "art/contact-sheet",
                      "kind": "texture",
                      "source": "textures/atlases/contact-sheet.png",
                      "status": "candidate",
                      "owner": "P1_CONTENT_ART",
                      "license": "CC0-1.0",
                      "attribution": "test",
                      "sourceUrl": null,
                      "provenance": "test",
                      "replacementCriterion": "replace",
                      "waiver": "test",
                      "width": 640,
                      "height": 360,
                      "metadata": null,
                      "sourceHash": null
                    }
                  ]
                }
                """);
            Directory.CreateDirectory(Path.Combine(fixture.SourceRoot, "textures", "atlases"));
            // Minimal valid 640x360 PNG (1x1 scaled via generator-less stub): write tiny then expect dimension fail
            // Use real repo contact sheet copy when available; otherwise skip file and expect missing-required.
            var contact = Path.Combine(RepositoryRoot, "content", "source", "textures", "atlases", "contact-sheet.png");
            if (File.Exists(contact))
                File.Copy(contact, Path.Combine(fixture.SourceRoot, "textures", "atlases", "contact-sheet.png"));
            return fixture;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, true);
        }
    }
}
