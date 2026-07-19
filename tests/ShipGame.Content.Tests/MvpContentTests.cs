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
    private static string GeneratedRoot => Path.Combine(RepositoryRoot, "content", "generated", "DesktopVK", "Content");

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
    public void CatalogSmokeValidatesRequiredMvpIdsWithoutHostWiring()
    {
        var catalog = MvpContentLoader.LoadAndValidate(SourceRoot, DefinitionsRoot);
        Assert.Contains("data/title-placeholder", catalog.AssetIds);
        Assert.Contains("MAT_FERRITE", catalog.DefinitionIds);
        Assert.Contains("projectiles/hostile", catalog.RegionIds);
        Assert.Contains("weapons/pulse", catalog.RegionIds);
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
    public void DataSourcesRemainFailClosedForNonDataKinds()
    {
        var manifest = new AssetManifest(
            ContractVersions.Content,
            [
                new("data/z", "data", "data/z.json", "placeholder", "test", "CC0-1.0"),
                new("atlases/a", "atlas", "textures/a.png", "candidate", "test", "CC0-1.0"),
                new("data/a", "data", "data/a.json", "placeholder", "test", "CC0-1.0")
            ]);

        var exception = Assert.Throws<ContentValidationException>(() => ContentBuildPlan.DataSources(manifest));
        Assert.Contains(exception.Issues, issue => issue.Code == "asset.unsupported-build-kind");

        var dataOnly = new AssetManifest(
            ContractVersions.Content,
            [
                new("data/z", "data", "data/z.json", "placeholder", "test", "CC0-1.0"),
                new("data/a", "data", "data/a.json", "placeholder", "test", "CC0-1.0")
            ]);
        Assert.Equal(
            ["data/asset-manifest.json", "data/a.json", "data/z.json"],
            ContentBuildPlan.DataSources(dataOnly));
    }

    [Fact]
    public void P0ManifestValidationRequiresAuthoringSourceEvenWhenXnbPresent()
    {
        var root = Path.Combine(Path.GetTempPath(), "ShipGame-P0Source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                """
                {"schemaVersion":1,"assets":[
                  {"id":"atlases/a","kind":"atlas","source":"textures/a.png","status":"candidate","owner":"test","license":"CC0-1.0"}
                ]}
                """);
            Directory.CreateDirectory(Path.Combine(root, "atlases"));
            File.WriteAllText(Path.Combine(root, "atlases", "a.xnb"), "xnb");
            var exception = Assert.Throws<ContentValidationException>(() =>
                ContentValidator.LoadAndValidateManifest(root, "manifest.json"));
            Assert.Contains(exception.Issues, issue => issue.Code == "asset.missing-source");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AnimationMetadataIsHonestSingleFrameUntilStripsArePacked()
    {
        var catalog = MvpContentLoader.LoadAndValidate(SourceRoot, DefinitionsRoot);
        foreach (var id in catalog.RegionIds)
        {
            var region = catalog.GetRegion(id);
            if (region.Animation is null)
                continue;
            Assert.InRange(region.Animation.Fps, 6, 12);
            Assert.True(region.Animation.Fps is 6 or 8 or 12);
            Assert.Single(region.Animation.Frames);
            Assert.Equal(0, region.Animation.Frames[0]);
        }
    }

    [Fact]
    public void GeneratedRootValidatesAuthoredSourcesAndCompiledXnb()
    {
        Assert.True(
            Directory.Exists(GeneratedRoot) &&
            File.Exists(Path.Combine(GeneratedRoot, "data", "asset-manifest.json")),
            "Run scripts/build-content.ps1 before this test.");

        MvpContentLoader.ValidateGeneratedRoot(GeneratedRoot, SourceRoot, DefinitionsRoot);

        var manifest = ContentValidator.LoadAndValidateManifest(GeneratedRoot, "data/asset-manifest.json");
        var catalog = new FileAssetCatalog(GeneratedRoot, manifest);
        Assert.Contains("Mine Your Own Business", catalog.LoadText(new ContentId("data/title-placeholder")), StringComparison.Ordinal);

        foreach (var asset in manifest.Assets.Where(item => item.Kind is "atlas" or "texture" or "sound"))
            Assert.True(
                File.Exists(Path.Combine(GeneratedRoot, asset.Id.Replace('/', Path.DirectorySeparatorChar) + ".xnb")),
                $"Missing compiled xnb for {asset.Id}");
    }

    [Fact]
    public void AtlasPixelGatesEnforcePaletteSilhouetteAlphaAndGrayscaleDistinctness()
    {
        var catalog = MvpContentLoader.LoadAndValidate(SourceRoot, DefinitionsRoot);
        var issues = new List<ValidationIssue>();

        foreach (var assetId in new[]
                 {
                     "atlases/player-modules", "atlases/enemies-telegraphs",
                     "atlases/asteroids-resources", "atlases/ui-icons", "art/contact-sheet"
                 })
        {
            var asset = catalog.GetAsset(assetId);
            var image = MvpPixelGates.LoadPng(Path.Combine(SourceRoot, asset.Source.Replace('/', Path.DirectorySeparatorChar)));
            MvpPixelGates.ValidatePaletteSize(image, assetId, issues);
        }

        var regionAssets = new (string AtlasId, string[] Regions)[]
        {
            ("atlases/asteroids-resources",
            [
                "asteroids/small/ordinary", "asteroids/small/ferrite", "asteroids/small/lumen",
                "asteroids/medium/ordinary", "asteroids/medium/ferrite", "asteroids/medium/lumen",
                "pickups/ferrite", "pickups/lumen", "pickups/data-core"
            ]),
            ("atlases/player-modules", ["weapons/pulse", "weapons/seeker", "ships/player/wayfarer"]),
            ("atlases/enemies-telegraphs", ["projectiles/hostile", "enemies/interceptor", "telegraphs/muzzle-flash"])
        };

        foreach (var (atlasId, regionIds) in regionAssets)
        {
            var asset = catalog.GetAsset(atlasId);
            var image = MvpPixelGates.LoadPng(Path.Combine(SourceRoot, asset.Source.Replace('/', Path.DirectorySeparatorChar)));
            var regions = regionIds.ToDictionary(id => id, catalog.GetRegion, StringComparer.Ordinal);
            foreach (var region in regions.Values)
                MvpPixelGates.ValidateRegionSilhouette(image, region, issues);

            if (atlasId == "atlases/asteroids-resources")
            {
                MvpPixelGates.ValidateGrayscaleDistinctPairs(
                    image,
                    regions,
                    [
                        ("asteroids/small/ordinary", "asteroids/small/ferrite", 8, 0.08),
                        ("asteroids/small/ordinary", "asteroids/small/lumen", 8, 0.08),
                        ("asteroids/small/ferrite", "asteroids/small/lumen", 8, 0.08),
                        ("asteroids/medium/ordinary", "asteroids/medium/ferrite", 8, 0.08),
                        ("asteroids/medium/ferrite", "asteroids/medium/lumen", 8, 0.08),
                        ("pickups/ferrite", "pickups/lumen", 8, 0.10)
                    ],
                    issues);
            }

            if (atlasId == "atlases/enemies-telegraphs")
            {
                var pulseAsset = catalog.GetAsset("atlases/player-modules");
                var pulseImage = MvpPixelGates.LoadPng(
                    Path.Combine(SourceRoot, pulseAsset.Source.Replace('/', Path.DirectorySeparatorChar)));
                var pulse = catalog.GetRegion("weapons/pulse");
                var hostile = catalog.GetRegion("projectiles/hostile");
                // Cross-atlas pair: compare luminance means + silhouette occupancy ratios.
                var pulseLum = MvpPixelGates.MeanOpaqueLuminance(pulseImage, pulse);
                var hostileLum = MvpPixelGates.MeanOpaqueLuminance(image, hostile);
                var pulseOpaque = CountOpaque(pulseImage, pulse);
                var hostileOpaque = CountOpaque(image, hostile);
                var occupancyDelta = Math.Abs(pulseOpaque - hostileOpaque);
                if (Math.Abs(pulseLum - hostileLum) < 12 && occupancyDelta < 0.15)
                {
                    issues.Add(new("art.grayscale-distinct",
                        $"Friendly weapons/pulse vs hostile projectiles/hostile not distinct in grayscale " +
                        $"(ΔL={Math.Abs(pulseLum - hostileLum):0.0}, occupancyΔ={occupancyDelta:0.000})."));
                }
            }
        }

        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(i => $"{i.Code}: {i.Message}")));
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

    private static double CountOpaque(MvpPixelGates.RgbaImage image, AtlasRegion region)
    {
        var opaque = 0;
        var total = region.Width * region.Height;
        for (var y = 0; y < region.Height; y++)
        for (var x = 0; x < region.Width; x++)
            if (image[region.X + x, region.Y + y].Opaque)
                opaque++;
        return opaque / (double)total;
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
