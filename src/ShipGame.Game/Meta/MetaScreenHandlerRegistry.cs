namespace ShipGame.Game;

public sealed class MetaScreenHandlerRegistry
{
    private readonly Dictionary<MetaScreen, IMetaScreenHandler> _handlers;

    public MetaScreenHandlerRegistry(IEnumerable<IMetaScreenHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        _handlers = new Dictionary<MetaScreen, IMetaScreenHandler>();
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            if (!_handlers.TryAdd(handler.Screen, handler))
                throw new ArgumentException($"Duplicate meta screen handler for '{handler.Screen}'.");
        }

        foreach (var screen in Enum.GetValues<MetaScreen>())
        {
            if (!_handlers.ContainsKey(screen))
                throw new ArgumentException($"Missing meta screen handler for '{screen}'.");
        }
    }

    public void BuildUi(MetaScreen screen, MetaUiContext context) =>
        _handlers[screen].BuildUi(context);

    public void Update(MetaScreen screen, MetaUiContext context) =>
        _handlers[screen].Update(context);

    public void Draw(MetaScreen screen, MetaDrawContext context) =>
        _handlers[screen].Draw(context);

    public void HandleHotkeys(MetaScreen screen, MetaUiContext context, Func<Microsoft.Xna.Framework.Input.Keys, bool> pressed) =>
        _handlers[screen].HandleHotkeys(context, pressed);

    public void DriveWindowSmoke(MetaScreen screen, MetaUiContext context, int ticks) =>
        _handlers[screen].DriveWindowSmoke(context, ticks);

    public static MetaScreenHandlerRegistry CreateDefault() => new(
    [
        new TitleMetaScreen(),
        new StationMetaScreen(),
        new MapMetaScreen(),
        new LoadoutMetaScreen(),
        new ResearchMetaScreen(),
        new UpgradesMetaScreen(),
        new SummaryMetaScreen(),
        new SettingsMetaScreen(),
        new PauseMetaScreen(),
        new RunMetaScreen()
    ]);
}
