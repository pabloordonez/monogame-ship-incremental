namespace ShipGame.Gameplay;

internal sealed class EnemyAiStrategyRegistry
{
    private readonly Dictionary<EnemyBehavior, IEnemyAiStrategy> _strategies;

    public EnemyAiStrategyRegistry(IEnumerable<IEnemyAiStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        _strategies = new Dictionary<EnemyBehavior, IEnemyAiStrategy>();
        foreach (var strategy in strategies)
        {
            ArgumentNullException.ThrowIfNull(strategy);
            if (!_strategies.TryAdd(strategy.Behavior, strategy))
                throw new ArgumentException($"Duplicate enemy AI strategy for '{strategy.Behavior}'.");
        }

        foreach (var behavior in Enum.GetValues<EnemyBehavior>())
        {
            if (!_strategies.ContainsKey(behavior))
                throw new ArgumentException($"Missing enemy AI strategy for '{behavior}'.");
        }
    }

    public IEnemyAiStrategy Get(EnemyBehavior behavior) => _strategies[behavior];

    public static EnemyAiStrategyRegistry CreateMvp() => new(
    [
        new InterceptorAiStrategy(),
        new GunshipAiStrategy(),
        new SapperAiStrategy()
    ]);
}
