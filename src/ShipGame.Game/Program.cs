namespace ShipGame.Game;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke", StringComparer.Ordinal))
            return SmokeRunner.Run();

        using var game = new ShipGameHost(args.Contains("--window-smoke", StringComparer.Ordinal));
        game.Run();
        return 0;
    }
}
