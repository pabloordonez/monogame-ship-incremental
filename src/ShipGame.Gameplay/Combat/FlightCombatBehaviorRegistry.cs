using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class FlightCombatBehaviorRegistry
{
    private readonly Dictionary<ContentId, WeaponDefinition> _weapons;
    private readonly Dictionary<ContentId, EnemyDefinition> _enemies;

    public FlightCombatBehaviorRegistry(
        IEnumerable<WeaponDefinition> weapons,
        IEnumerable<EnemyDefinition> enemies)
    {
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(enemies);
        _weapons = Materialize(weapons, "weapon");
        _enemies = Materialize(enemies, "enemy");
        if (_weapons.Count > 64 || _enemies.Count > 64)
            throw new ArgumentException("P2 behavior registries are bounded to 64 entries per kind.");
        Validate();
    }

    public WeaponDefinition Weapon(ContentId id) =>
        _weapons.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown weapon behavior '{id}'.");

    public EnemyDefinition Enemy(ContentId id) =>
        _enemies.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Unknown enemy behavior '{id}'.");

    public static FlightCombatBehaviorRegistry CreateMvp() => new(
        [
            new(new ContentId("MOD_WEAPON_PULSE"), WeaponBehavior.Pulse, 10, 12, 700, 650),
            // Upgrade vs pulse (~50 DPS): 100 DPS hitscan, wider cone, longer sustain before overheat.
            new(new ContentId("MOD_WEAPON_BEAM"), WeaponBehavior.Beam, 100, 1, 0, 600, 0.5f, 3f, LockConeDegrees: 24),
            // 0.6s salvo; free-fires straight when no cone lock (see SeekerWeaponFireStrategy).
            new(new ContentId("MOD_WEAPON_SEEKER"), WeaponBehavior.Seeker, 16, 36, 480, 600, BurstCount: 2, LockConeDegrees: 35, TurnDegreesPerSecond: 150)
        ],
        [
            new(new ContentId("ENM_INTERCEPTOR"), EnemyBehavior.Interceptor, 28, 190, 120, 6, 132),
            new(new ContentId("ENM_GUNSHIP"), EnemyBehavior.Gunship, 55, 105, 380, 18, 168),
            new(new ContentId("ENM_SAPPER"), EnemyBehavior.Sapper, 42, 130, 260, 24, 210)
        ]);

    private static Dictionary<ContentId, T> Materialize<T>(
        IEnumerable<T> values,
        string kind) where T : struct
    {
        var result = new Dictionary<ContentId, T>();
        foreach (var value in values)
        {
            var id = value switch
            {
                WeaponDefinition weapon => weapon.Id,
                EnemyDefinition enemy => enemy.Id,
                _ => throw new InvalidOperationException($"Unsupported {kind} definition.")
            };
            if (!result.TryAdd(id, value))
                throw new ArgumentException($"Duplicate {kind} behavior ID '{id}'.");
        }
        return result;
    }

    private void Validate()
    {
        foreach (var value in _weapons.Values)
        {
            if (!float.IsFinite(value.Damage) || value.Damage <= 0 ||
                value.CadenceTicks <= 0 || !float.IsFinite(value.Range) || value.Range <= 0 ||
                value.BurstCount is < 1 or > 8)
                throw new ArgumentException($"Weapon '{value.Id}' has invalid bounded values.");
        }
        foreach (var value in _enemies.Values)
        {
            if (!float.IsFinite(value.Hull) || value.Hull <= 0 ||
                !float.IsFinite(value.Speed) || value.Speed <= 0 ||
                value.CadenceTicks <= 0)
                throw new ArgumentException($"Enemy '{value.Id}' has invalid bounded values.");
        }
    }
}
