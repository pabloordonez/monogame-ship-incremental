using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public static class FlightCombatConstants
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;
    public const short CommandScale = 10_000;
    public const int MaximumEntities = 2_048;
    public const int MaximumEventsPerTick = 4_096;
    public const int MaximumDamageRequestsPerTick = 4_096;
    /// <summary>Inclusive future span accepted by <see cref="FlightCombatSimulation.Queue"/> (current tick + this many).</summary>
    public const int CommandHorizonTicks = TickRate * 10;
    public const int CommandSlotCount = CommandHorizonTicks + 1;
}

[Flags]
public enum FlightAction : byte
{
    None = 0,
    Fire = 1,
    Mine = 2,
    Mobility = 4,
    Interact = 8
}

public readonly record struct FlightCommandFrame(
    long TargetTick,
    short MoveX,
    short MoveY,
    short AimX,
    short AimY,
    FlightAction Actions)
{
    public static FlightCommandFrame Neutral(long tick) => new(tick, 0, 0, 0, 0, FlightAction.None);

    public Vector2 Move => Decode(MoveX, MoveY);
    public Vector2 Aim => Decode(AimX, AimY);

    public static short Quantize(float value) =>
        (short)Math.Clamp(
            (int)MathF.Round(Math.Clamp(value, -1f, 1f) * FlightCombatConstants.CommandScale),
            -FlightCombatConstants.CommandScale,
            FlightCombatConstants.CommandScale);

    private static Vector2 Decode(short x, short y)
    {
        var value = new Vector2(x, y) / FlightCombatConstants.CommandScale;
        var lengthSquared = value.LengthSquared();
        return lengthSquared > 1f ? Vector2.Normalize(value) : value;
    }
}

public enum Faction : byte
{
    Player,
    Hostile,
    Neutral
}

public enum WeaponBehavior : byte
{
    Pulse,
    Beam,
    Seeker
}

public enum MobilityBehavior : byte
{
    Dash,
    Blink
}

public enum EnemyBehavior : byte
{
    Interceptor,
    Gunship,
    Sapper
}

public readonly record struct Transform2(Vector2 Position, float Rotation);
public readonly record struct Velocity2(Vector2 Value);
public readonly record struct Collider(float Radius, uint Layer, uint Mask, bool BlocksMovement = true);
public readonly record struct FlightStatistics(float Acceleration, float Braking, float MaximumSpeed);
public readonly record struct PlayerControlled;
public readonly record struct ControlIntent(Vector2 Move, Vector2 Aim, FlightAction Actions, FlightAction PreviousActions);
public readonly record struct Combatant(Faction Faction);
public readonly record struct Health(float Current, float Maximum);
public readonly record struct Shield(float Current, float Maximum, float RechargePerSecond, int RechargeDelayTicks, int TicksSinceDamage);
public readonly record struct Invulnerability(int TicksRemaining);
public readonly record struct Destroyed(long Tick);
public readonly record struct ContactDamage(float Damage);

public readonly record struct WeaponMount(ContentId BehaviorId, WeaponBehavior Behavior);
public readonly record struct WeaponState(int CooldownTicks, float Heat, bool HeatLocked, EntityId Target);
public readonly record struct DamageSource(EntityId Owner, Faction Faction, float Damage, bool Projectile);
public readonly record struct Projectile(int LifetimeTicks, float RemainingPierces, bool IsMissile);
public readonly record struct Homing(EntityId Target, float Speed, float TurnRadiansPerSecond);

public readonly record struct MobilityAbility(
    ContentId BehaviorId,
    MobilityBehavior Behavior,
    float Distance,
    int DurationTicks,
    int CooldownTicks,
    int CooldownRemaining,
    int ActiveTicksRemaining,
    Vector2 Direction);

public readonly record struct TemporaryCombatModifiers(
    float DamageMultiplier,
    float FireRateMultiplier,
    float SpeedMultiplier,
    float MobilityCooldownMultiplier,
    int ExtraProjectiles,
    float ExtraProjectileDamageMultiplier,
    int PierceCount,
    bool ShockTransit);

public readonly record struct PendingTemporaryModifier(TemporaryCombatModifiers Value);
public readonly record struct AiBrain(EnemyBehavior Behavior, int StateTicks, int BurstShotsRemaining, int ActiveMines);
public readonly record struct Target(EntityId Entity);
public readonly record struct ThreatValue(int Value);
public readonly record struct Elite(float DamageMultiplier);
public readonly record struct SpawnAnchor(Vector2 Position, bool OutsideCamera);
public readonly record struct Mine(int ArmTicks, int LifetimeTicks, float Radius, float Damage, EntityId Owner);

public readonly record struct WeaponDefinition(
    ContentId Id,
    WeaponBehavior Behavior,
    float Damage,
    int CadenceTicks,
    float ProjectileSpeed,
    float Range,
    float HeatPerTick = 0,
    float CoolPerTick = 0,
    int BurstCount = 1,
    float LockConeDegrees = 0,
    float TurnDegreesPerSecond = 0);

public readonly record struct EnemyDefinition(
    ContentId Id,
    EnemyBehavior Behavior,
    float Hull,
    float Speed,
    float PreferredRange,
    float Damage,
    int CadenceTicks);

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
            new(new ContentId("MOD_WEAPON_BEAM"), WeaponBehavior.Beam, 30, 1, 0, 520, 1, 1.5f),
            new(new ContentId("MOD_WEAPON_SEEKER"), WeaponBehavior.Seeker, 16, 144, 360, 600, BurstCount: 2, LockConeDegrees: 35, TurnDegreesPerSecond: 150)
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

public enum CombatEventKind : byte
{
    WeaponFired,
    CollisionDetected,
    ShieldDamaged,
    ShieldDepleted,
    HullDamaged,
    EntityDestroyed,
    AbilityActivated,
    AbilityRejected,
    EnemySpawned,
    EliteActivated,
    MineTelegraphed,
    CommandRejected
}

public readonly record struct CombatEvent(
    CombatEventKind Kind,
    long Tick,
    EntityId Entity,
    EntityId Other,
    ContentId ContentId,
    Vector2 Position,
    float Amount,
    float Remaining,
    string Detail)
{
    public static CombatEvent Create(
        CombatEventKind kind,
        long tick,
        EntityId entity = default,
        EntityId other = default,
        ContentId contentId = default,
        Vector2 position = default,
        float amount = 0,
        float remaining = 0,
        string detail = "") =>
        new(kind, tick, entity, other, contentId, position, amount, remaining, detail);
}

public readonly record struct CombatSnapshot(
    long Tick,
    EntityId Entity,
    Vector2 Position,
    float Rotation,
    Faction Faction,
    float Hull,
    float Shield,
    bool Destroyed);
