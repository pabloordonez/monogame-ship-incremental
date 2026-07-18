using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

internal static class SourceAssetGenerator
{
    private static readonly Rgba[] Palette =
    [
        new(0, 0, 0, 0), new(6, 10, 22), new(16, 25, 46), new(42, 55, 79),
        new(91, 107, 127), new(190, 207, 214), new(244, 241, 222), new(43, 205, 219),
        new(96, 239, 255), new(237, 75, 55), new(255, 137, 60), new(255, 214, 92),
        new(70, 190, 102), new(145, 232, 126), new(137, 85, 211), new(207, 126, 255)
    ];

    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Generate(string sourceRoot)
    {
        var textureRoot = Path.Combine(sourceRoot, "textures", "atlases");
        var backgroundRoot = Path.Combine(sourceRoot, "textures", "backgrounds");
        var dataRoot = Path.Combine(sourceRoot, "data");
        var sfxRoot = Path.Combine(sourceRoot, "sfx");
        Directory.CreateDirectory(textureRoot);
        Directory.CreateDirectory(backgroundRoot);
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(sfxRoot);

        WriteAtlas(
            Path.Combine(textureRoot, "player-modules.png"),
            Path.Combine(dataRoot, "atlas-player-modules.json"),
            "atlases/player-modules",
            PlayerModules(),
            PlayerCollisions());
        WriteAtlas(
            Path.Combine(textureRoot, "enemies-telegraphs.png"),
            Path.Combine(dataRoot, "atlas-enemies-telegraphs.json"),
            "atlases/enemies-telegraphs",
            EnemyTelegraphs(),
            EnemyCollisions());
        WriteAtlas(
            Path.Combine(textureRoot, "asteroids-resources.png"),
            Path.Combine(dataRoot, "atlas-asteroids-resources.json"),
            "atlases/asteroids-resources",
            AsteroidsResources(),
            AsteroidCollisions());
        WriteAtlas(
            Path.Combine(textureRoot, "ui-icons.png"),
            Path.Combine(dataRoot, "atlas-ui-icons.json"),
            "atlases/ui-icons",
            UiIcons(),
            []);
        WriteBackground(Path.Combine(backgroundRoot, "cinder-belt.png"), warm: true);
        WriteBackground(Path.Combine(backgroundRoot, "ion-veil.png"), warm: false);
        WriteContactSheet(Path.Combine(textureRoot, "contact-sheet.png"));
        WriteWav(Path.Combine(sfxRoot, "essential-cues.wav"));
    }

    private static List<RegionDef> PlayerModules() =>
    [
        Region("ships/player/wayfarer", 64, 64, Family.Player, 0, hardpoints: WayfarerHardpoints(), collision: "collision/wayfarer"),
        Region("ships/player/engine", 64, 64, Family.Player, 1, animation: Anim(8, 3)),
        Region("ships/player/damage-flash", 64, 64, Family.Player, 2, animation: Anim(12, 2)),
        Region("ships/player/shield-mask", 64, 64, Family.Player, 3),
        Region("ships/player/dash", 64, 64, Family.Player, 4, animation: Anim(12, 4)),
        Region("ships/player/blink", 64, 64, Family.Player, 5, animation: Anim(12, 4)),
        Region("ships/utility/firefly", 32, 32, Family.Player, 6, animation: Anim(8, 2), collision: "collision/projectile"),
        Region("weapons/pulse", 16, 16, Family.Weapon, 0, collision: "collision/projectile"),
        Region("weapons/beam", 16, 32, Family.Weapon, 1, animation: Anim(12, 2)),
        Region("weapons/seeker", 12, 12, Family.Weapon, 2, animation: Anim(8, 2), collision: "collision/projectile"),
        Region("weapons/mining-beam", 16, 32, Family.Weapon, 3, animation: Anim(12, 2)),
        Region("weapons/seismic-charge", 16, 16, Family.Weapon, 4, animation: Anim(6, 3), collision: "collision/projectile"),
        Region("effects/tractor", 32, 32, Family.Player, 7, animation: Anim(8, 3))
    ];

    private static List<RegionDef> EnemyTelegraphs() =>
    [
        Region("enemies/interceptor", 32, 32, Family.Enemy, 0, animation: Anim(8, 2), collision: "collision/enemy-small",
            hardpoints: Dict(("primaryWeapon", 16, 4), ("leftEngine", 8, 26), ("rightEngine", 24, 26))),
        Region("enemies/gunship", 64, 64, Family.Enemy, 1, animation: Anim(6, 2), collision: "collision/enemy-medium",
            hardpoints: Dict(("primaryWeapon", 32, 8))),
        Region("enemies/sapper", 48, 48, Family.Enemy, 2, animation: Anim(8, 2), collision: "collision/enemy-small",
            hardpoints: Dict(("utility", 24, 36))),
        Region("enemies/elite-outline", 64, 64, Family.Enemy, 3, animation: Anim(8, 2)),
        Region("telegraphs/elite-marker", 32, 32, Family.Telegraph, 0, animation: Anim(6, 3)),
        Region("telegraphs/muzzle-flash", 32, 32, Family.Telegraph, 1, animation: Anim(12, 2)),
        Region("telegraphs/aim-line", 64, 16, Family.Telegraph, 2, animation: Anim(12, 2)),
        Region("telegraphs/mine-radius", 64, 64, Family.Telegraph, 3, animation: Anim(8, 3)),
        Region("effects/enemy-destruction", 64, 64, Family.Enemy, 4, animation: Anim(12, 6))
    ];

    private static List<RegionDef> AsteroidsResources() =>
    [
        Region("asteroids/small/ordinary", 32, 32, Family.Asteroid, 0, collision: "collision/asteroid-small"),
        Region("asteroids/small/ferrite", 32, 32, Family.Asteroid, 1, collision: "collision/asteroid-small"),
        Region("asteroids/small/lumen", 32, 32, Family.Asteroid, 2, collision: "collision/asteroid-small"),
        Region("asteroids/medium/ordinary", 64, 64, Family.Asteroid, 3, collision: "collision/asteroid-medium"),
        Region("asteroids/medium/ferrite", 64, 64, Family.Asteroid, 4, collision: "collision/asteroid-medium"),
        Region("asteroids/medium/lumen", 64, 64, Family.Asteroid, 5, collision: "collision/asteroid-medium"),
        Region("asteroids/large/ordinary", 96, 96, Family.Asteroid, 6, collision: "collision/asteroid-large"),
        Region("asteroids/large/ferrite", 96, 96, Family.Asteroid, 7, collision: "collision/asteroid-large"),
        Region("asteroids/large/lumen", 96, 96, Family.Asteroid, 8, collision: "collision/asteroid-large"),
        Region("asteroids/break", 32, 32, Family.Asteroid, 9, animation: Anim(8, 4)),
        Region("pickups/ferrite", 10, 10, Family.Pickup, 0, animation: Anim(8, 2), collision: "collision/pickup"),
        Region("pickups/lumen", 10, 10, Family.Pickup, 1, animation: Anim(8, 2), collision: "collision/pickup"),
        Region("pickups/data-core", 12, 12, Family.Pickup, 2, animation: Anim(8, 3), collision: "collision/pickup"),
        Region("pickups/upgrade-charge", 12, 12, Family.Pickup, 3, animation: Anim(8, 2), collision: "collision/pickup"),
        Region("field/extraction-marker", 32, 32, Family.Field, 0, animation: Anim(6, 3)),
        Region("field/cover-cue", 32, 32, Family.Field, 1),
        Region("hazards/solar-flare", 64, 32, Family.Hazard, 0, animation: Anim(12, 3)),
        Region("hazards/ion-cloud", 48, 48, Family.Hazard, 1, animation: Anim(8, 3)),
        Region("hazards/star-glow", 48, 48, Family.Hazard, 2, animation: Anim(6, 2))
    ];

    private static List<RegionDef> UiIcons()
    {
        string[] ids =
        [
            "ui/icons/resource-ferrite", "ui/icons/resource-lumen", "ui/icons/resource-data-core", "ui/icons/lock",
            "ui/icons/module-weapon", "ui/icons/module-mining", "ui/icons/module-shield", "ui/icons/module-engine",
            "ui/icons/module-utility", "ui/icons/upgrade-damage", "ui/icons/upgrade-rate", "ui/icons/upgrade-fork",
            "ui/icons/upgrade-pierce", "ui/icons/upgrade-shield", "ui/icons/upgrade-reboot", "ui/icons/upgrade-hull",
            "ui/icons/upgrade-speed", "ui/icons/upgrade-mobility", "ui/icons/upgrade-mining", "ui/icons/upgrade-tractor",
            "ui/icons/upgrade-shock", "ui/icons/research-hull", "ui/icons/research-shield", "ui/icons/research-beam",
            "ui/icons/research-seeker", "ui/icons/research-mining", "ui/icons/research-assay", "ui/icons/research-engine",
            "ui/icons/research-blink", "ui/icons/research-drone", "ui/icons/research-tractor", "ui/icons/research-ion",
            "ui/icons/research-recovery", "ui/icons/objective", "ui/icons/interact", "ui/icons/pause",
            "ui/icons/input-keyboard", "ui/icons/input-gamepad", "ui/icons/hull", "ui/icons/shield"
        ];
        return ids.Select((id, index) => Region(id, 32, 32, Family.Ui, index)).ToList();
    }

    private static List<object> PlayerCollisions() =>
    [
        Collision("collision/wayfarer", "circle", [32, 32, 18]),
        Collision("collision/projectile", "circle", [8, 8, 3])
    ];

    private static List<object> EnemyCollisions() =>
    [
        Collision("collision/enemy-small", "circle", [16, 16, 12]),
        Collision("collision/enemy-medium", "circle", [32, 32, 20])
    ];

    private static List<object> AsteroidCollisions() =>
    [
        Collision("collision/asteroid-small", "circle", [16, 16, 12]),
        Collision("collision/asteroid-medium", "circle", [32, 32, 24]),
        Collision("collision/asteroid-large", "circle", [48, 48, 40]),
        Collision("collision/pickup", "circle", [6, 6, 5])
    ];

    private static Dictionary<string, object> WayfarerHardpoints() => Dict(
        ("primaryWeapon", 32, 8),
        ("miningTool", 18, 18),
        ("utility", 46, 28),
        ("leftEngine", 20, 52),
        ("rightEngine", 44, 52),
        ("shieldOrigin", 32, 32));

    private static void WriteAtlas(
        string pngPath,
        string jsonPath,
        string textureAssetId,
        List<RegionDef> regions,
        List<object> collisions)
    {
        const int atlasSize = 512;
        const int gap = 5;
        Pack(regions, atlasSize, gap);
        var image = new Image(atlasSize, atlasSize);
        foreach (var region in regions)
            DrawRegion(image, region);

        Png.Write(pngPath, image);
        var document = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["textureAssetId"] = textureAssetId,
            ["width"] = atlasSize,
            ["height"] = atlasSize,
            ["padding"] = 2,
            ["extrusion"] = 1,
            ["rotatedPacking"] = false,
            ["collisions"] = collisions,
            ["regions"] = regions.Select(region => new Dictionary<string, object?>
            {
                ["id"] = region.Id,
                ["x"] = region.X,
                ["y"] = region.Y,
                ["width"] = region.Width,
                ["height"] = region.Height,
                ["pivotX"] = 0.5,
                ["pivotY"] = 0.5,
                ["collision"] = region.Collision,
                ["hardpoints"] = region.Hardpoints,
                ["animation"] = region.Animation
            }).ToList()
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(document, JsonWrite) + Environment.NewLine);
    }

    private static void Pack(List<RegionDef> regions, int atlasSize, int gap)
    {
        var ordered = regions.OrderByDescending(region => region.Height * 1000 + region.Width).ToList();
        var x = gap;
        var y = gap;
        var rowHeight = 0;
        foreach (var region in ordered)
        {
            if (x + region.Width + gap > atlasSize)
            {
                x = gap;
                y += rowHeight + gap;
                rowHeight = 0;
            }
            if (y + region.Height + gap > atlasSize)
                throw new InvalidOperationException($"Atlas overflow packing '{region.Id}'.");
            region.X = x;
            region.Y = y;
            x += region.Width + gap;
            rowHeight = Math.Max(rowHeight, region.Height);
        }

        // Restore stable catalog order for JSON readability.
        var positions = ordered.ToDictionary(region => region.Id);
        foreach (var region in regions)
        {
            var packed = positions[region.Id];
            region.X = packed.X;
            region.Y = packed.Y;
        }
    }

    private static void DrawRegion(Image image, RegionDef region)
    {
        var cx = region.X + region.Width / 2;
        var cy = region.Y + region.Height / 2;
        var primary = Palette[7 + ((region.Seed + (int)region.Family) % 9)];
        var secondary = Palette[3 + ((region.Seed * 3 + (int)region.Family) % 4)];
        var accent = region.Family is Family.Enemy or Family.Telegraph or Family.Hazard ? Palette[9] : primary;

        switch (region.Family)
        {
            case Family.Player:
                image.Diamond(cx, cy, Math.Max(4, region.Width / 3), Math.Max(4, region.Height / 3), Palette[5]);
                image.Rect(cx - 2, region.Y + 2, 5, Math.Max(6, region.Height / 2), primary);
                image.Rect(region.X + 4, region.Y + region.Height - 10, 6, 6, Palette[8]);
                image.Rect(region.X + region.Width - 10, region.Y + region.Height - 10, 6, 6, Palette[8]);
                break;
            case Family.Weapon:
                image.Rect(cx - Math.Max(1, region.Width / 4), cy - Math.Max(1, region.Height / 4),
                    Math.Max(2, region.Width / 2), Math.Max(2, region.Height / 2), primary);
                image.Rect(cx - 1, cy - 1, 2, 2, Palette[6]);
                break;
            case Family.Enemy:
                image.Rect(region.X + 2, cy - 3, region.Width - 4, 7, accent);
                image.Rect(cx - 3, region.Y + 2, 7, region.Height - 4, secondary);
                image.Rect(cx - 1, cy - 1, 3, 3, Palette[6]);
                break;
            case Family.Telegraph:
                image.Rect(region.X + 1, cy - 1, region.Width - 2, 3, Palette[10]);
                image.Rect(cx - 1, region.Y + 1, 3, region.Height - 2, Palette[10]);
                break;
            case Family.Asteroid:
                var radius = Math.Max(4, Math.Min(region.Width, region.Height) / 2 - 2);
                image.Diamond(cx, cy, radius, radius - 1, secondary);
                if (region.Id.Contains("ferrite", StringComparison.Ordinal))
                    image.Rect(cx - 2, cy - 2, 4, 4, Palette[11]);
                if (region.Id.Contains("lumen", StringComparison.Ordinal))
                    image.Rect(cx - 2, cy - 2, 4, 4, Palette[14]);
                break;
            case Family.Pickup:
                image.Diamond(cx, cy, region.Width / 2 - 1, region.Height / 2 - 1, primary);
                image.Rect(cx - 1, cy - 1, 2, 2, Palette[6]);
                break;
            case Family.Field:
                image.Rect(region.X + 4, region.Y + 4, region.Width - 8, region.Height - 8, Palette[11]);
                image.Rect(cx - 1, region.Y + 2, 3, region.Height - 4, Palette[6]);
                break;
            case Family.Hazard:
                image.Diamond(cx, cy, region.Width / 3, region.Height / 3, accent);
                image.Rect(region.X + 2, cy, region.Width - 4, 2, Palette[10]);
                break;
            case Family.Ui:
                image.Diamond(cx, cy, 10, 10, primary);
                image.Rect(cx - 2, cy - 8, 5, 17, Palette[1]);
                image.Rect(cx - 8, cy - 2, 17, 5, Palette[1]);
                if (region.Seed % 2 == 0)
                    image.Rect(cx - 2, cy - 2, 5, 5, Palette[6]);
                break;
        }
    }

    private static void WriteBackground(string path, bool warm)
    {
        var image = new Image(640, 360, Palette[1]);
        for (var i = 0; i < 150; i++)
        {
            var x = (i * 97 + 31) % image.Width;
            var y = (i * 53 + 17) % image.Height;
            var color = i % 9 == 0 ? (warm ? Palette[10] : Palette[15]) : Palette[4];
            image.Rect(x, y, i % 13 == 0 ? 2 : 1, i % 13 == 0 ? 2 : 1, color);
        }
        var glow = warm ? Palette[10] : Palette[7];
        image.Diamond(warm ? 90 : 530, warm ? 80 : 110, 25, 25, glow);
        image.Diamond(warm ? 90 : 530, warm ? 80 : 110, 10, 10, Palette[6]);
        Png.Write(path, image);
    }

    private static void WriteContactSheet(string path)
    {
        var image = new Image(640, 360, Palette[1]);
        for (var i = 1; i < Palette.Length; i++)
            image.Rect(16 + (i - 1) * 38, 16, 32, 32, Palette[i]);
        DrawSized(image, 40, 80, 64, 64, Family.Player, 0);
        DrawSized(image, 120, 80, 32, 32, Family.Enemy, 0);
        DrawSized(image, 170, 80, 64, 64, Family.Enemy, 1);
        DrawSized(image, 250, 80, 48, 48, Family.Enemy, 2);
        DrawSized(image, 320, 80, 32, 32, Family.Asteroid, 0);
        DrawSized(image, 370, 80, 64, 64, Family.Asteroid, 3);
        DrawSized(image, 450, 80, 96, 96, Family.Asteroid, 6);
        DrawSized(image, 40, 200, 10, 10, Family.Pickup, 0);
        DrawSized(image, 70, 200, 12, 12, Family.Pickup, 2);
        DrawSized(image, 110, 200, 32, 32, Family.Ui, 0);
        DrawSized(image, 160, 200, 24, 24, Family.Ui, 1);
        Png.Write(path, image);
    }

    private static void DrawSized(Image image, int x, int y, int width, int height, Family family, int seed)
    {
        var region = Region("preview", width, height, family, seed);
        region.X = x;
        region.Y = y;
        DrawRegion(image, region);
    }

    private static void WriteWav(string path)
    {
        const int sampleRate = 22050;
        const int cueSamples = 1102;
        const int cueCount = 12;
        var samples = new short[cueSamples * cueCount];
        for (var cue = 0; cue < cueCount; cue++)
        for (var i = 0; i < cueSamples; i++)
        {
            var envelope = 1.0 - i / (double)cueSamples;
            var frequency = 180 + cue * 55;
            samples[cue * cueSamples + i] = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * 9000);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples.Length * 2);
        foreach (var sample in samples)
            writer.Write(sample);
    }

    private static RegionDef Region(
        string id,
        int width,
        int height,
        Family family,
        int seed,
        object? animation = null,
        string? collision = null,
        Dictionary<string, object>? hardpoints = null) =>
        new(id, width, height, family, seed, animation, collision, hardpoints);

    private static object Anim(int fps, int frames) => new Dictionary<string, object>
    {
        ["fps"] = fps,
        ["frames"] = Enumerable.Range(0, frames).ToArray()
    };

    private static Dictionary<string, object> Dict(params (string Name, int X, int Y)[] points) =>
        points.ToDictionary(
            point => point.Name,
            point => (object)new Dictionary<string, int> { ["x"] = point.X, ["y"] = point.Y },
            StringComparer.Ordinal);

    private static object Collision(string id, string kind, double[] values) => new Dictionary<string, object>
    {
        ["id"] = id,
        ["kind"] = kind,
        ["values"] = values
    };

    private enum Family
    {
        Player,
        Weapon,
        Enemy,
        Telegraph,
        Asteroid,
        Pickup,
        Field,
        Hazard,
        Ui
    }

    private sealed class RegionDef(
        string id,
        int width,
        int height,
        Family family,
        int seed,
        object? animation,
        string? collision,
        Dictionary<string, object>? hardpoints)
    {
        public string Id { get; } = id;
        public int Width { get; } = width;
        public int Height { get; } = height;
        public Family Family { get; } = family;
        public int Seed { get; } = seed;
        public object? Animation { get; } = animation;
        public string? Collision { get; } = collision;
        public Dictionary<string, object>? Hardpoints { get; } = hardpoints;
        public int X { get; set; }
        public int Y { get; set; }
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A = 255);

    private sealed class Image
    {
        private readonly Rgba[] _pixels;
        public Image(int width, int height, Rgba? clear = null)
        {
            Width = width;
            Height = height;
            _pixels = Enumerable.Repeat(clear ?? Palette[0], width * height).ToArray();
        }
        public int Width { get; }
        public int Height { get; }
        public ReadOnlySpan<Rgba> Pixels => _pixels;
        public void Rect(int x, int y, int width, int height, Rgba color)
        {
            for (var py = Math.Max(0, y); py < Math.Min(Height, y + height); py++)
            for (var px = Math.Max(0, x); px < Math.Min(Width, x + width); px++)
                _pixels[py * Width + px] = color;
        }
        public void Diamond(int cx, int cy, int rx, int ry, Rgba color)
        {
            for (var y = -ry; y <= ry; y++)
            {
                var span = (int)Math.Floor(rx * (1.0 - Math.Abs(y) / (double)Math.Max(1, ry)));
                Rect(cx - span, cy + y, span * 2 + 1, 1, color);
            }
        }
    }

    private static class Png
    {
        private static readonly uint[] CrcTable = BuildCrcTable();
        public static void Write(string path, Image image)
        {
            using var stream = File.Create(path);
            stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            Span<byte> header = stackalloc byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header[..4], image.Width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..8], image.Height);
            header[8] = 8;
            header[9] = 6;
            WriteChunk(stream, "IHDR", header);

            using var raw = new MemoryStream();
            using (var zlib = new ZLibStream(raw, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                var pixels = image.Pixels;
                for (var y = 0; y < image.Height; y++)
                {
                    zlib.WriteByte(0);
                    for (var x = 0; x < image.Width; x++)
                    {
                        var pixel = pixels[y * image.Width + x];
                        zlib.WriteByte(pixel.R);
                        zlib.WriteByte(pixel.G);
                        zlib.WriteByte(pixel.B);
                        zlib.WriteByte(pixel.A);
                    }
                }
            }
            WriteChunk(stream, "IDAT", raw.ToArray());
            WriteChunk(stream, "IEND", []);
        }
        private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> data)
        {
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
            stream.Write(length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            var crc = 0xffffffffu;
            foreach (var value in typeBytes)
                crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
            foreach (var value in data)
                crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
            Span<byte> crcBytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xffffffffu);
            stream.Write(crcBytes);
        }
        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < table.Length; n++)
            {
                var value = n;
                for (var k = 0; k < 8; k++)
                    value = (value & 1) != 0 ? 0xedb88320u ^ (value >> 1) : value >> 1;
                table[n] = value;
            }
            return table;
        }
    }
}
