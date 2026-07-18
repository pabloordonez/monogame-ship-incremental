using System.Buffers.Binary;
using System.IO.Compression;

namespace ShipGame.Content;

/// <summary>
/// P1-owned automated art gates: palette size, alpha/silhouette occupancy, and
/// grayscale-luminance distinctness for key region families.
/// </summary>
public static class MvpPixelGates
{
    public const int MaxPaletteColors = 32;

    public readonly record struct Rgba(byte R, byte G, byte B, byte A)
    {
        public bool Opaque => A >= 128;
        public byte Luminance => (byte)Math.Clamp((int)Math.Round(0.2126 * R + 0.7152 * G + 0.0722 * B), 0, 255);
    }

    public sealed class RgbaImage
    {
        public RgbaImage(int width, int height, Rgba[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public int Width { get; }
        public int Height { get; }
        public Rgba[] Pixels { get; }

        public Rgba this[int x, int y] => Pixels[y * Width + x];
    }

    public static RgbaImage LoadPng(string path)
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
            var type = System.Text.Encoding.ASCII.GetString(typeBytes);
            var data = new byte[length];
            if (length > 0 && stream.Read(data) != length)
                throw new InvalidDataException($"Truncated PNG chunk {type} in {path}");
            stream.Position += 4; // CRC
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

        var pixels = new Rgba[width * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            if (raw[row] != 0)
                throw new InvalidDataException($"Filtered PNG rows are unsupported in {path}.");
            for (var x = 0; x < width; x++)
            {
                var i = row + 1 + x * channels;
                var a = channels == 4 ? raw[i + 3] : (byte)255;
                pixels[y * width + x] = new Rgba(raw[i], raw[i + 1], raw[i + 2], a);
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    public static int CountOpaquePaletteColors(RgbaImage image)
    {
        var colors = new HashSet<int>();
        foreach (var pixel in image.Pixels)
        {
            if (!pixel.Opaque)
                continue;
            colors.Add(pixel.R << 16 | pixel.G << 8 | pixel.B);
        }
        return colors.Count;
    }

    public static void ValidatePaletteSize(RgbaImage image, string assetId, List<ValidationIssue> issues)
    {
        var count = CountOpaquePaletteColors(image);
        if (count > MaxPaletteColors)
            issues.Add(new("art.palette-size", $"Asset '{assetId}' uses {count} opaque colors (max {MaxPaletteColors})."));
    }

    public static void ValidateRegionSilhouette(
        RgbaImage atlas,
        AtlasRegion region,
        List<ValidationIssue> issues)
    {
        var opaque = 0;
        var transparent = 0;
        var edgeOpaque = 0;
        var edgeTotal = 0;
        for (var y = 0; y < region.Height; y++)
        for (var x = 0; x < region.Width; x++)
        {
            var px = atlas[region.X + x, region.Y + y];
            if (px.Opaque)
                opaque++;
            else
                transparent++;
            if (x == 0 || y == 0 || x == region.Width - 1 || y == region.Height - 1)
            {
                edgeTotal++;
                if (px.Opaque)
                    edgeOpaque++;
            }
        }

        var area = region.Width * region.Height;
        if (opaque < Math.Max(4, area / 32))
            issues.Add(new("art.silhouette", $"Region '{region.Id}' has insufficient opaque silhouette ({opaque}/{area})."));
        if (transparent < Math.Max(2, area / 64))
            issues.Add(new("art.alpha", $"Region '{region.Id}' lacks surrounding/interior alpha ({transparent}/{area})."));
        // Soft edge check: at least some border transparency for non-full-bleed icons.
        if (region.Width >= 16 && region.Height >= 16 && edgeOpaque == edgeTotal)
            issues.Add(new("art.alpha-edge", $"Region '{region.Id}' is fully opaque to its bounds (no silhouette edge)."));
    }

    public static double MeanOpaqueLuminance(RgbaImage atlas, AtlasRegion region)
    {
        long sum = 0;
        var count = 0;
        for (var y = 0; y < region.Height; y++)
        for (var x = 0; x < region.Width; x++)
        {
            var px = atlas[region.X + x, region.Y + y];
            if (!px.Opaque)
                continue;
            sum += px.Luminance;
            count++;
        }
        return count == 0 ? 0 : sum / (double)count;
    }

    public static double SilhouetteHammingRatio(RgbaImage atlas, AtlasRegion a, AtlasRegion b)
    {
        var width = Math.Min(a.Width, b.Width);
        var height = Math.Min(a.Height, b.Height);
        if (width <= 0 || height <= 0)
            return 1;
        var differing = 0;
        var total = width * height;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var oa = atlas[a.X + x, a.Y + y].Opaque;
            var ob = atlas[b.X + x, b.Y + y].Opaque;
            if (oa != ob)
                differing++;
        }
        return differing / (double)total;
    }

    public static void ValidateGrayscaleDistinctPairs(
        RgbaImage atlas,
        IReadOnlyDictionary<string, AtlasRegion> regions,
        IEnumerable<(string Left, string Right, double MinLuminanceDelta, double MinSilhouetteDelta)> pairs,
        List<ValidationIssue> issues)
    {
        foreach (var (left, right, minLum, minSilhouette) in pairs)
        {
            if (!regions.TryGetValue(left, out var a) || !regions.TryGetValue(right, out var b))
            {
                issues.Add(new("art.missing-region", $"Grayscale gate missing region '{left}' or '{right}'."));
                continue;
            }

            var lumDelta = Math.Abs(MeanOpaqueLuminance(atlas, a) - MeanOpaqueLuminance(atlas, b));
            var silhouette = SilhouetteHammingRatio(atlas, a, b);
            if (lumDelta < minLum && silhouette < minSilhouette)
            {
                issues.Add(new("art.grayscale-distinct",
                    $"Regions '{left}' vs '{right}' are not distinct in grayscale " +
                    $"(ΔL={lumDelta:0.0}, silhouette={silhouette:0.000}; need ΔL>={minLum} or silhouette>={minSilhouette})."));
            }
        }
    }
}
