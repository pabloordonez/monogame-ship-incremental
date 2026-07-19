using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

/// <summary>
/// Packs authored per-region sprites into atlas PNGs + metadata JSON.
/// Does not invent pixel art; sprites live under content/source/textures/sprites/.
/// </summary>
internal static class SourceAssetGenerator
{
    private static readonly JsonSerializerOptions JsonRead = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Generate(string sourceRoot) => PackAtlases(sourceRoot);

    public static void PackAtlases(string sourceRoot)
    {
        var catalogPath = Path.Combine(sourceRoot, "data", "atlas-pack-catalog.json");
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException("Missing atlas pack catalog.", catalogPath);

        var catalog = JsonSerializer.Deserialize<PackCatalog>(File.ReadAllText(catalogPath), JsonRead)
            ?? throw new InvalidDataException("atlas-pack-catalog.json is empty.");

        var spriteRoot = Path.Combine(sourceRoot, catalog.SpriteRoot.Replace('/', Path.DirectorySeparatorChar));
        var atlasRoot = Path.Combine(sourceRoot, catalog.AtlasRoot.Replace('/', Path.DirectorySeparatorChar));
        var dataRoot = Path.Combine(sourceRoot, "data");
        Directory.CreateDirectory(atlasRoot);
        Directory.CreateDirectory(dataRoot);

        foreach (var atlas in catalog.Atlases)
        {
            PackAtlas(atlas, spriteRoot, atlasRoot, dataRoot, catalog.AtlasSize, catalog.Gap, catalog.Padding, catalog.Extrusion);
        }

        var dataRootForSheet = Path.Combine(sourceRoot, "data");
        WriteContactSheet(
            Path.Combine(atlasRoot, "contact-sheet.png"),
            atlasRoot,
            dataRootForSheet,
            catalog);
        Console.WriteLine($"Packed {catalog.Atlases.Count} atlases from authored sprites.");
    }

    private static void PackAtlas(
        AtlasPackDef atlas,
        string spriteRoot,
        string atlasRoot,
        string dataRoot,
        int atlasSize,
        int gap,
        int padding,
        int extrusion)
    {
        var regions = atlas.Regions.Select(region =>
        {
            var spritePath = Path.Combine(spriteRoot, region.Id.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (!File.Exists(spritePath))
                throw new FileNotFoundException($"Missing authored sprite for '{region.Id}'.", spritePath);

            var sprite = Png.Read(spritePath);
            if (sprite.Width != region.Width || sprite.Height != region.Height)
            {
                throw new InvalidDataException(
                    $"Sprite '{region.Id}' is {sprite.Width}x{sprite.Height}, expected {region.Width}x{region.Height}.");
            }

            return new PackedRegion(region, sprite);
        }).ToList();

        PackShelf(regions, atlasSize, gap);
        var image = new Image(atlasSize, atlasSize);
        foreach (var region in regions)
            image.Blit(region.Sprite, region.X, region.Y);

        var pngPath = Path.Combine(atlasRoot, atlas.AtlasFile);
        var jsonPath = Path.Combine(dataRoot, atlas.MetadataFile);
        Png.Write(pngPath, image);

        var document = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["textureAssetId"] = atlas.TextureAssetId,
            ["width"] = atlasSize,
            ["height"] = atlasSize,
            ["padding"] = padding,
            ["extrusion"] = extrusion,
            ["rotatedPacking"] = false,
            ["collisions"] = atlas.Collisions,
            ["regions"] = regions.Select(region => new Dictionary<string, object?>
            {
                ["id"] = region.Def.Id,
                ["x"] = region.X,
                ["y"] = region.Y,
                ["width"] = region.Def.Width,
                ["height"] = region.Def.Height,
                ["pivotX"] = 0.5,
                ["pivotY"] = 0.5,
                ["collision"] = region.Def.Collision,
                ["hardpoints"] = region.Def.Hardpoints,
                ["animation"] = region.Def.Animation
            }).ToList()
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(document, JsonWrite) + Environment.NewLine);
    }

    private static void PackShelf(List<PackedRegion> regions, int atlasSize, int gap)
    {
        var ordered = regions.OrderByDescending(region => region.Def.Height * 1000 + region.Def.Width).ToList();
        var x = gap;
        var y = gap;
        var rowHeight = 0;
        foreach (var region in ordered)
        {
            if (x + region.Def.Width + gap > atlasSize)
            {
                x = gap;
                y += rowHeight + gap;
                rowHeight = 0;
            }
            if (y + region.Def.Height + gap > atlasSize)
                throw new InvalidOperationException($"Atlas overflow packing '{region.Def.Id}'.");
            region.X = x;
            region.Y = y;
            x += region.Def.Width + gap;
            rowHeight = Math.Max(rowHeight, region.Def.Height);
        }

        var positions = ordered.ToDictionary(region => region.Def.Id);
        foreach (var region in regions)
        {
            var packed = positions[region.Def.Id];
            region.X = packed.X;
            region.Y = packed.Y;
        }
    }

    private static void WriteContactSheet(string path, string atlasRoot, string dataRoot, PackCatalog catalog)
    {
        var image = new Image(640, 360, new Rgba(6, 10, 22));
        Rgba[] swatches =
        [
            new(16, 25, 46), new(42, 55, 79), new(91, 107, 127), new(190, 207, 214),
            new(244, 241, 222), new(43, 205, 219), new(96, 239, 255), new(237, 75, 55),
            new(255, 137, 60), new(255, 214, 92), new(70, 190, 102), new(145, 232, 126),
            new(137, 85, 211), new(207, 126, 255)
        ];
        for (var i = 0; i < swatches.Length; i++)
            image.Rect(16 + i * 38, 16, 32, 32, swatches[i]);

        var samples = new (string Atlas, string Id, int X, int Y)[]
        {
            ("player-modules.png", "ships/player/wayfarer", 40, 80),
            ("enemies-telegraphs.png", "enemies/interceptor", 120, 80),
            ("enemies-telegraphs.png", "enemies/gunship", 170, 80),
            ("enemies-telegraphs.png", "enemies/sapper", 250, 80),
            ("asteroids-resources.png", "asteroids/small/ordinary", 320, 80),
            ("asteroids-resources.png", "asteroids/medium/ordinary", 370, 80),
            ("asteroids-resources.png", "asteroids/large/ordinary", 450, 80),
            ("asteroids-resources.png", "pickups/ferrite", 40, 200),
            ("asteroids-resources.png", "pickups/data-core", 70, 200),
            ("ui-icons.png", "ui/icons/resource-ferrite", 110, 200),
            ("ui-icons.png", "ui/icons/shield", 160, 200)
        };

        var atlasCache = new Dictionary<string, (Image Image, Dictionary<string, (int X, int Y, int W, int H)> Regions)>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            if (!atlasCache.TryGetValue(sample.Atlas, out var packed))
            {
                var atlasPath = Path.Combine(atlasRoot, sample.Atlas);
                var metaName = catalog.Atlases.First(a => a.AtlasFile == sample.Atlas).MetadataFile;
                var metaPath = Path.Combine(dataRoot, metaName);
                var atlasImage = Png.Read(atlasPath);
                var meta = JsonSerializer.Deserialize<AtlasMetaDoc>(File.ReadAllText(metaPath), JsonRead)
                    ?? throw new InvalidDataException(metaPath);
                var map = meta.Regions.ToDictionary(
                    r => r.Id,
                    r => (r.X, r.Y, r.Width, r.Height),
                    StringComparer.Ordinal);
                packed = (atlasImage, map);
                atlasCache[sample.Atlas] = packed;
            }

            if (!packed.Regions.TryGetValue(sample.Id, out var rect))
                continue;
            packed.Image.CopyRegion(image, rect.X, rect.Y, rect.W, rect.H, sample.X, sample.Y);
        }

        Png.Write(path, image);
    }

    private sealed class PackCatalog
    {
        public string SpriteRoot { get; set; } = "textures/sprites";
        public string AtlasRoot { get; set; } = "textures/atlases";
        public int AtlasSize { get; set; } = 512;
        public int Gap { get; set; } = 5;
        public int Padding { get; set; } = 2;
        public int Extrusion { get; set; } = 1;
        public List<AtlasPackDef> Atlases { get; set; } = [];
    }

    private sealed class AtlasPackDef
    {
        public string TextureAssetId { get; set; } = "";
        public string AtlasFile { get; set; } = "";
        public string MetadataFile { get; set; } = "";
        public List<object> Collisions { get; set; } = [];
        public List<RegionPackDef> Regions { get; set; } = [];
    }

    private sealed class RegionPackDef
    {
        public string Id { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string? Collision { get; set; }
        public object? Hardpoints { get; set; }
        public object? Animation { get; set; }
    }

    private sealed class AtlasMetaDoc
    {
        public List<AtlasMetaRegion> Regions { get; set; } = [];
    }

    private sealed class AtlasMetaRegion
    {
        public string Id { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private sealed class PackedRegion(RegionPackDef def, Image sprite)
    {
        public RegionPackDef Def { get; } = def;
        public Image Sprite { get; } = sprite;
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
            _pixels = Enumerable.Repeat(clear ?? default, width * height).ToArray();
        }
        public int Width { get; }
        public int Height { get; }
        public Rgba[] Pixels => _pixels;
        public Rgba Get(int x, int y) => _pixels[y * Width + x];
        public void Set(int x, int y, Rgba color)
        {
            if ((uint)x < (uint)Width && (uint)y < (uint)Height)
                _pixels[y * Width + x] = color;
        }
        public void Rect(int x, int y, int width, int height, Rgba color)
        {
            for (var py = Math.Max(0, y); py < Math.Min(Height, y + height); py++)
            for (var px = Math.Max(0, x); px < Math.Min(Width, x + width); px++)
                _pixels[py * Width + px] = color;
        }
        public void Blit(Image source, int destX, int destY)
        {
            for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var px = source.Get(x, y);
                if (px.A == 0)
                    continue;
                Set(destX + x, destY + y, px);
            }
        }
        public void CopyRegion(Image dest, int srcX, int srcY, int width, int height, int destX, int destY)
        {
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if ((uint)(srcX + x) >= (uint)Width || (uint)(srcY + y) >= (uint)Height)
                    continue;
                var px = Get(srcX + x, srcY + y);
                if (px.A == 0)
                    continue;
                dest.Set(destX + x, destY + y, px);
            }
        }
    }

    private static class Png
    {
        private static readonly uint[] CrcTable = BuildCrcTable();

        public static Image Read(string path)
        {
            using var stream = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[8];
            if (stream.Read(signature) != 8 || !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                throw new InvalidDataException($"Not a PNG: {path}");

            var width = 0;
            var height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            Span<byte> lengthBytes = stackalloc byte[4];
            Span<byte> typeBytes = stackalloc byte[4];
            using var idat = new MemoryStream();
            while (stream.Position < stream.Length)
            {
                if (stream.Read(lengthBytes) != 4)
                    break;
                var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
                if (stream.Read(typeBytes) != 4)
                    break;
                var type = Encoding.ASCII.GetString(typeBytes);
                var data = new byte[length];
                if (length > 0 && stream.Read(data) != length)
                    throw new InvalidDataException($"Truncated PNG chunk {type} in {path}");
                stream.Position += 4;
                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                    bitDepth = data[8];
                    colorType = data[9];
                }
                else if (type == "IDAT")
                    idat.Write(data);
                else if (type == "IEND")
                    break;
            }

            if (width <= 0 || height <= 0 || bitDepth != 8 || colorType is not (2 or 6))
                throw new InvalidDataException($"Unsupported PNG format in {path} (need 8-bit RGB/RGBA).");

            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            var channels = colorType == 6 ? 4 : 3;
            var stride = 1 + width * channels;
            var raw = new byte[stride * height];
            var read = 0;
            while (read < raw.Length)
            {
                var n = zlib.Read(raw, read, raw.Length - read);
                if (n == 0)
                    break;
                read += n;
            }
            if (read != raw.Length)
                throw new InvalidDataException($"Incomplete PNG pixel data in {path}.");

            var image = new Image(width, height);
            var prior = new byte[width * channels];
            var current = new byte[width * channels];
            for (var y = 0; y < height; y++)
            {
                var row = y * stride;
                var filter = raw[row];
                var src = raw.AsSpan(row + 1, width * channels);
                for (var i = 0; i < current.Length; i++)
                {
                    var left = i >= channels ? current[i - channels] : (byte)0;
                    var up = prior[i];
                    var upLeft = i >= channels ? prior[i - channels] : (byte)0;
                    current[i] = filter switch
                    {
                        0 => src[i],
                        1 => (byte)(src[i] + left),
                        2 => (byte)(src[i] + up),
                        3 => (byte)(src[i] + ((left + up) / 2)),
                        4 => (byte)(src[i] + Paeth(left, up, upLeft)),
                        _ => throw new InvalidDataException($"Unsupported PNG filter {filter} in {path}.")
                    };
                }

                for (var x = 0; x < width; x++)
                {
                    var i = x * channels;
                    var a = channels == 4 ? current[i + 3] : (byte)255;
                    image.Set(x, y, new Rgba(current[i], current[i + 1], current[i + 2], a));
                }

                (prior, current) = (current, prior);
            }
            return image;
        }

        private static byte Paeth(byte a, byte b, byte c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
                return a;
            return pb <= pc ? b : c;
        }

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
                for (var y = 0; y < image.Height; y++)
                {
                    zlib.WriteByte(0);
                    for (var x = 0; x < image.Width; x++)
                    {
                        var pixel = image.Get(x, y);
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
