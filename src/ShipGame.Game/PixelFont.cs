using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace ShipGame.Game;

/// <summary>
/// Tiny procedural 5×7 bitmap font so MVP UI is readable without SpriteFont content.
/// </summary>
public sealed class PixelFont : IDisposable
{
    public const int GlyphWidth = 5;
    public const int GlyphHeight = 7;
    public const int CellWidth = 6;
    public const int CellHeight = 8;

    private readonly Texture2D _atlas;
    private readonly Dictionary<char, XnaRectangle> _glyphs = new();

    public PixelFont(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        // 16×6 grid covers ASCII 32..127
        const int cols = 16;
        const int rows = 6;
        _atlas = new Texture2D(device, cols * CellWidth, rows * CellHeight);
        var pixels = new XnaColor[_atlas.Width * _atlas.Height];

        for (var code = 32; code < 128; code++)
        {
            var index = code - 32;
            var col = index % cols;
            var row = index / cols;
            var dest = new XnaRectangle(col * CellWidth, row * CellHeight, GlyphWidth, GlyphHeight);
            _glyphs[(char)code] = dest;
            BlitGlyph(pixels, _atlas.Width, dest.X, dest.Y, GlyphPatterns.For((char)code));
        }

        _atlas.SetData(pixels);
    }

    public int MeasureWidth(string text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length * CellWidth;

    public void Draw(
        SpriteBatch spriteBatch,
        string text,
        int x,
        int y,
        XnaColor color,
        int scale = 1)
    {
        ArgumentNullException.ThrowIfNull(spriteBatch);
        if (string.IsNullOrEmpty(text))
            return;
        scale = Math.Max(1, scale);
        var cursorX = x;
        foreach (var character in text)
        {
            if (character == '\n')
            {
                cursorX = x;
                y += CellHeight * scale;
                continue;
            }

            var key = character is >= (char)32 and < (char)128 ? character : '?';
            var source = _glyphs[key];
            spriteBatch.Draw(
                _atlas,
                new XnaRectangle(cursorX, y, GlyphWidth * scale, GlyphHeight * scale),
                source,
                color);
            cursorX += CellWidth * scale;
        }
    }

    public void Dispose() => _atlas.Dispose();

    private static void BlitGlyph(XnaColor[] pixels, int stride, int originX, int originY, ulong bits)
    {
        for (var row = 0; row < GlyphHeight; row++)
        {
            for (var col = 0; col < GlyphWidth; col++)
            {
                var bitIndex = row * GlyphWidth + col;
                if (((bits >> (GlyphWidth * GlyphHeight - 1 - bitIndex)) & 1UL) == 0)
                    continue;
                pixels[(originY + row) * stride + (originX + col)] = XnaColor.White;
            }
        }
    }
}

internal static class GlyphPatterns
{
    // 5×7 bitmaps packed MSB-first row-major. Enough alphabet/digits/punctuation for MVP UI.
    public static ulong For(char character) => character switch
    {
        ' ' => 0,
        '!' => 0b00100_00100_00100_00100_00100_00000_00100UL,
        '#' => 0b01010_01010_11111_01010_11111_01010_01010UL,
        '%' => 0b11001_11010_00100_01000_10110_10011_00000UL,
        '+' => 0b00000_00100_00100_11111_00100_00100_00000UL,
        '-' => 0b00000_00000_00000_11111_00000_00000_00000UL,
        '.' => 0b00000_00000_00000_00000_00000_00100_00100UL,
        '/' => 0b00001_00010_00100_01000_10000_00000_00000UL,
        '0' => 0b01110_10001_10011_10101_11001_10001_01110UL,
        '1' => 0b00100_01100_00100_00100_00100_00100_01110UL,
        '2' => 0b01110_10001_00001_00010_00100_01000_11111UL,
        '3' => 0b01110_10001_00001_00110_00001_10001_01110UL,
        '4' => 0b00010_00110_01010_10010_11111_00010_00010UL,
        '5' => 0b11111_10000_11110_00001_00001_10001_01110UL,
        '6' => 0b00110_01000_10000_11110_10001_10001_01110UL,
        '7' => 0b11111_00001_00010_00100_01000_01000_01000UL,
        '8' => 0b01110_10001_10001_01110_10001_10001_01110UL,
        '9' => 0b01110_10001_10001_01111_00001_00010_01100UL,
        ':' => 0b00000_00100_00100_00000_00100_00100_00000UL,
        '<' => 0b00010_00100_01000_10000_01000_00100_00010UL,
        '=' => 0b00000_00000_11111_00000_11111_00000_00000UL,
        '>' => 0b01000_00100_00010_00001_00010_00100_01000UL,
        '?' => 0b01110_10001_00001_00010_00100_00000_00100UL,
        'A' => 0b01110_10001_10001_11111_10001_10001_10001UL,
        'B' => 0b11110_10001_10001_11110_10001_10001_11110UL,
        'C' => 0b01110_10001_10000_10000_10000_10001_01110UL,
        'D' => 0b11110_10001_10001_10001_10001_10001_11110UL,
        'E' => 0b11111_10000_10000_11110_10000_10000_11111UL,
        'F' => 0b11111_10000_10000_11110_10000_10000_10000UL,
        'G' => 0b01110_10001_10000_10111_10001_10001_01110UL,
        'H' => 0b10001_10001_10001_11111_10001_10001_10001UL,
        'I' => 0b01110_00100_00100_00100_00100_00100_01110UL,
        'J' => 0b00111_00010_00010_00010_00010_10010_01100UL,
        'K' => 0b10001_10010_10100_11000_10100_10010_10001UL,
        'L' => 0b10000_10000_10000_10000_10000_10000_11111UL,
        'M' => 0b10001_11011_10101_10101_10001_10001_10001UL,
        'N' => 0b10001_11001_10101_10011_10001_10001_10001UL,
        'O' => 0b01110_10001_10001_10001_10001_10001_01110UL,
        'P' => 0b11110_10001_10001_11110_10000_10000_10000UL,
        'Q' => 0b01110_10001_10001_10001_10101_10010_01101UL,
        'R' => 0b11110_10001_10001_11110_10100_10010_10001UL,
        'S' => 0b01110_10001_10000_01110_00001_10001_01110UL,
        'T' => 0b11111_00100_00100_00100_00100_00100_00100UL,
        'U' => 0b10001_10001_10001_10001_10001_10001_01110UL,
        'V' => 0b10001_10001_10001_10001_10001_01010_00100UL,
        'W' => 0b10001_10001_10001_10101_10101_10101_01010UL,
        'X' => 0b10001_10001_01010_00100_01010_10001_10001UL,
        'Y' => 0b10001_10001_01010_00100_00100_00100_00100UL,
        'Z' => 0b11111_00001_00010_00100_01000_10000_11111UL,
        '[' => 0b01110_01000_01000_01000_01000_01000_01110UL,
        ']' => 0b01110_00010_00010_00010_00010_00010_01110UL,
        '_' => 0b00000_00000_00000_00000_00000_00000_11111UL,
        'a' => 0b00000_00000_01110_00001_01111_10001_01111UL,
        'b' => 0b10000_10000_11110_10001_10001_10001_11110UL,
        'c' => 0b00000_00000_01110_10001_10000_10001_01110UL,
        'd' => 0b00001_00001_01111_10001_10001_10001_01111UL,
        'e' => 0b00000_00000_01110_10001_11111_10000_01110UL,
        'f' => 0b00110_01000_01000_11100_01000_01000_01000UL,
        'g' => 0b00000_01111_10001_10001_01111_00001_01110UL,
        'h' => 0b10000_10000_11110_10001_10001_10001_10001UL,
        'i' => 0b00100_00000_01100_00100_00100_00100_01110UL,
        'j' => 0b00010_00000_00110_00010_00010_10010_01100UL,
        'k' => 0b10000_10000_10010_10100_11000_10100_10010UL,
        'l' => 0b01100_00100_00100_00100_00100_00100_01110UL,
        'm' => 0b00000_00000_11010_10101_10101_10001_10001UL,
        'n' => 0b00000_00000_11110_10001_10001_10001_10001UL,
        'o' => 0b00000_00000_01110_10001_10001_10001_01110UL,
        'p' => 0b00000_11110_10001_10001_11110_10000_10000UL,
        'q' => 0b00000_01111_10001_10001_01111_00001_00001UL,
        'r' => 0b00000_00000_10110_11001_10000_10000_10000UL,
        's' => 0b00000_00000_01111_10000_01110_00001_11110UL,
        't' => 0b01000_01000_11100_01000_01000_01000_00110UL,
        'u' => 0b00000_00000_10001_10001_10001_10001_01111UL,
        'v' => 0b00000_00000_10001_10001_10001_01010_00100UL,
        'w' => 0b00000_00000_10001_10001_10101_10101_01010UL,
        'x' => 0b00000_00000_10001_01010_00100_01010_10001UL,
        'y' => 0b00000_10001_10001_10001_01111_00001_01110UL,
        'z' => 0b00000_00000_11111_00010_00100_01000_11111UL,
        _ => 0b11111_10001_10101_10101_10101_10001_11111UL
    };
}
