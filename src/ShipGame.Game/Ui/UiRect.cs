namespace ShipGame.Game;

public readonly record struct UiRect(int X, int Y, int Width, int Height)
{
    public bool Contains(int x, int y) =>
        x >= X && y >= Y && x < X + Width && y < Y + Height;
}
