namespace ShipGame.Simulation;

internal sealed class WeaponStrategyRegistry
{
    private readonly Dictionary<WeaponBehavior, IWeaponFireStrategy> _strategies;

    public WeaponStrategyRegistry(IEnumerable<IWeaponFireStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = new Dictionary<WeaponBehavior, IWeaponFireStrategy>();
        foreach (var strategy in strategies)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            if (!_strategies.TryAdd(strategy.Behavior, strategy))
                throw new ArgumentException($"Duplicate weapon fire strategy for '{strategy.Behavior}'.");
        }

        foreach (var behavior in Enum.GetValues<WeaponBehavior>())
        {
            if (!_strategies.ContainsKey(behavior))
                throw new ArgumentException($"Missing weapon fire strategy for '{behavior}'.");
        }
    }

    public IWeaponFireStrategy Get(WeaponBehavior behavior) => _strategies[behavior];

    public static WeaponStrategyRegistry CreateMvp() => new(
    [
        new PulseWeaponFireStrategy(),
        new BeamWeaponFireStrategy(),
        new SeekerWeaponFireStrategy()
    ]);
}
