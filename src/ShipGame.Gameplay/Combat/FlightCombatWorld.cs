using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public sealed class FlightCombatWorld
{
    private readonly FlightCombatContext _context;
    private readonly SystemScheduler _scheduler = new();

    public FlightCombatWorld(
        ulong seed,
        FlightCombatBehaviorRegistry? registry = null)
    {
        _context = new FlightCombatContext(seed, registry);
        _scheduler.Add(new ApplyFlightCombatStructuralChangesSystem(_context));
        _scheduler.Add(new ConsumeFlightCommandsSystem(_context));
        _scheduler.Add(new AdvanceCombatTimersSystem(_context));
        _scheduler.Add(new ConsumeTemporaryModifiersSystem(_context));
        _scheduler.Add(new AiAndThreatDecisionsSystem(_context));
        _scheduler.Add(new ResolveMobilitySystem(_context));
        _scheduler.Add(new IntegrateFlightMovementSystem(_context));
        _scheduler.Add(new RebuildCombatSpatialIndexSystem(_context));
        _scheduler.Add(new DetectCombatCollisionsSystem(_context));
        _scheduler.Add(new ResolveWeaponsSystem(_context));
        _scheduler.Add(new ResolveMinesSystem(_context));
        _scheduler.Add(new ResolveOrderedDamageSystem(_context));
        _scheduler.Add(new PublishCombatEventsAndHashSystem(_context));
        Schedule = _scheduler.Order;
    }

    public long Tick => _context.Tick;
    public ulong LastStateHash => _context.LastStateHash;
    public EntityId Player => _context.Player;
    public IReadOnlyList<CombatEvent> Events => _context.EventView;
    public IReadOnlyList<string> Schedule { get; }

    public bool Queue(FlightCommandFrame command)
    {
        if (command.TargetTick < Tick ||
            command.TargetTick > Tick + FlightCombatConstants.CommandHorizonTicks)
        {
            _context.AddEvent(CombatEvent.Create(
                CombatEventKind.CommandRejected,
                Tick,
                amount: command.TargetTick,
                detail: command.TargetTick < Tick ? "stale" : "future"));
            _context.LastStateHash = _context.CalculateHash();
            return false;
        }

        var slot = FlightCombatContext.CommandSlot(command.TargetTick);
        if (_context.CommandOccupied[slot] && _context.CommandSlots[slot].TargetTick != command.TargetTick)
            throw new InvalidOperationException("Command slot map collision within the accepted horizon.");
        if (!_context.CommandOccupied[slot])
        {
            _context.CommandOccupied[slot] = true;
            _context.PendingCommandCount++;
        }
        _context.CommandSlots[slot] = command;
        _context.LastStateHash = _context.CalculateHash();
        return true;
    }

    public EntityId SpawnPlayer(
        Vector2 position,
        ContentId weaponId,
        MobilityBehavior mobility = MobilityBehavior.Dash,
        PlayerSpawnStats? stats = null)
    {
        if (_context.Player != default && _context.World.IsAlive(_context.Player))
            throw new InvalidOperationException("Only one player is supported.");
        var entity = _context.CreateEntity();
        _ = new PlayerShip(
            _context.World,
            entity,
            position,
            weaponId,
            _context.Registry.Weapon(weaponId),
            mobility,
            stats ?? PlayerSpawnStats.Defaults);
        _context.Player = entity;
        return entity;
    }

    public EntityId SpawnEnemy(ContentId enemyId, Vector2 position, bool elite = false) =>
        _context.SpawnEnemy(enemyId, position, elite);

    public EntityId SpawnObstacle(Vector2 position, float radius)
    {
        var entity = _context.CreateEntity();
        _ = new Obstacle(_context.World, entity, position, radius);
        return entity;
    }

    /// <summary>
    /// Marks an entity for removal on the next structural pass without emitting combat events
    /// (used when mined asteroids tear down their mirrored obstacles).
    /// </summary>
    public void DestroyEntity(EntityId entity)
    {
        if (entity == default || !_context.World.IsAlive(entity) || _context.Has<Destroyed>(entity))
            return;
        _context.World.Set(entity, new Destroyed(_context.Tick));
        _context.PendingDestroy.Add(entity);
    }

    public void AddSpawnAnchor(Vector2 position, bool outsideCamera = true)
    {
        if (_context.Anchors.Count >= 64)
            throw new InvalidOperationException("Threat anchors are bounded to 64.");
        _context.Anchors.Add(new SpawnAnchor(position, outsideCamera));
    }

    public void ConfigureThreatDirector(int intervalTicks, int activeCap)
    {
        if (intervalTicks <= 0 || activeCap is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(intervalTicks));
        _context.ThreatEnabled = true;
        _context.ThreatIntervalTicks = intervalTicks;
        _context.ThreatCap = activeCap;
    }

    public void GrantTemporaryModifiers(TemporaryCombatModifiers modifiers)
    {
        EnsurePlayer();
        FlightCombatContext.ValidateModifiers(modifiers);
        _context.World.Set(_context.Player, modifiers);
        if (_context.World.Store<PendingTemporaryModifier>().Has(_context.Player))
            _context.World.Remove<PendingTemporaryModifier>(_context.Player);
    }

    public void ClearTemporaryModifiers()
    {
        EnsurePlayer();
        _context.World.Set(_context.Player, FlightCombatContext.DefaultModifiers());
        if (_context.World.Store<PendingTemporaryModifier>().Has(_context.Player))
            _context.World.Remove<PendingTemporaryModifier>(_context.Player);
    }

    public CombatSnapshot Snapshot(EntityId entity)
    {
        if (!_context.World.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is stale or dead.");
        var transform = _context.World.Store<Transform2>().Has(entity)
            ? _context.World.Store<Transform2>().Read(entity)
            : default;
        var faction = _context.World.Store<Combatant>().Has(entity)
            ? _context.World.Store<Combatant>().Read(entity).Faction
            : Faction.Neutral;
        var hull = _context.World.Store<Health>().Has(entity) ? _context.World.Store<Health>().Read(entity).Current : 0;
        var shield = _context.World.Store<Shield>().Has(entity) ? _context.World.Store<Shield>().Read(entity).Current : 0;
        return new CombatSnapshot(
            Tick,
            entity,
            transform.Position,
            transform.Rotation,
            faction,
            hull,
            shield,
            _context.World.Store<Destroyed>().Has(entity));
    }

    /// <summary>Ordered live combat snapshots for presentation (P5).</summary>
    public void CollectSnapshots(List<CombatSnapshot> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        _context.RebuildSortedLive();
        for (var i = 0; i < _context.SortedLive.Count; i++)
        {
            var entity = _context.SortedLive[i];
            if (!_context.World.IsAlive(entity) || !_context.Has<Transform2>(entity))
                continue;
            into.Add(Snapshot(entity));
        }
    }

    /// <summary>Presentation-oriented entity list with render kind (skips destroyed).</summary>
    public void CollectRenderItems(List<CombatRenderItem> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        _context.RebuildSortedLive();
        for (var i = 0; i < _context.SortedLive.Count; i++)
        {
            var entity = _context.SortedLive[i];
            if (!_context.World.IsAlive(entity) || !_context.Has<Transform2>(entity) || _context.Has<Destroyed>(entity))
                continue;
            var transform = _context.World.Get<Transform2>(entity);
            var faction = _context.Has<Combatant>(entity) ? _context.World.Get<Combatant>(entity).Faction : Faction.Neutral;
            var kind = CombatRenderKind.Obstacle;
            if (_context.Has<Projectile>(entity))
                kind = CombatRenderKind.Projectile;
            else if (_context.Has<Mine>(entity))
                kind = CombatRenderKind.Mine;
            else if (faction == Faction.Player && _context.Has<PlayerControlled>(entity))
                kind = CombatRenderKind.PlayerShip;
            else if (faction == Faction.Hostile)
                kind = CombatRenderKind.EnemyShip;
            else
                continue;

            var archetype = default(ContentId);
            if (kind == CombatRenderKind.EnemyShip && _context.Has<WeaponMount>(entity))
                archetype = _context.World.Get<WeaponMount>(entity).BehaviorId;

            var isMissile = kind == CombatRenderKind.Projectile &&
                            _context.Has<Projectile>(entity) &&
                            _context.World.Get<Projectile>(entity).IsMissile;
            into.Add(new(
                entity,
                transform.Position,
                transform.Rotation,
                faction,
                kind,
                _context.Has<Elite>(entity),
                _context.Has<Health>(entity) ? _context.World.Get<Health>(entity).Current : 0,
                _context.Has<Shield>(entity) ? _context.World.Get<Shield>(entity).Current : 0,
                archetype,
                isMissile));
        }
    }

    public bool IsElite(EntityId entity) =>
        entity != default && _context.World.IsAlive(entity) && _context.Has<Elite>(entity);

    /// <summary>True for hostile ships (not projectiles/mines/obstacles), including the destroy tick.</summary>
    public bool IsHostileShip(EntityId entity) =>
        entity != default &&
        _context.World.IsAlive(entity) &&
        _context.Has<Combatant>(entity) &&
        _context.World.Get<Combatant>(entity).Faction == Faction.Hostile &&
        _context.Has<Health>(entity) &&
        !_context.Has<Projectile>(entity) &&
        !_context.Has<Mine>(entity);

    public bool TryGetPosition(EntityId entity, out Vector2 position)
    {
        position = default;
        if (entity == default || !_context.World.IsAlive(entity) || !_context.Has<Transform2>(entity))
            return false;
        position = _context.World.Get<Transform2>(entity).Position;
        return true;
    }

    public bool TryGetPlayerAim(out Vector2 aim)
    {
        aim = Vector2.UnitX;
        if (_context.Player == default || !_context.World.IsAlive(_context.Player) || !_context.Has<ControlIntent>(_context.Player))
            return false;
        aim = _context.World.Get<ControlIntent>(_context.Player).Aim;
        if (aim.LengthSquared() < 0.0001f)
            aim = Vector2.UnitX;
        else
            aim = Vector2.Normalize(aim);
        return true;
    }

    public bool TryGetPlayerWeapon(out WeaponBehavior behavior, out float range)
    {
        behavior = default;
        range = 0f;
        if (_context.Player == default ||
            !_context.World.IsAlive(_context.Player) ||
            !_context.Has<WeaponMount>(_context.Player))
            return false;
        var mount = _context.World.Get<WeaponMount>(_context.Player);
        behavior = mount.Behavior;
        range = _context.Registry.Weapon(mount.BehaviorId).Range;
        return true;
    }

    /// <summary>
    /// When the beam is locked and the aim ray clips the target collider, returns distance along aim
    /// to the surface entry. Cone-only locks (ray miss) return false so presentation does not shorten
    /// the beam into empty space.
    /// </summary>
    public bool TryGetPlayerBeamHitDistance(out float distance)
    {
        distance = 0f;
        if (_context.Player == default ||
            !_context.World.IsAlive(_context.Player) ||
            !_context.Has<WeaponState>(_context.Player) ||
            !_context.Has<Transform2>(_context.Player))
            return false;
        var target = _context.World.Get<WeaponState>(_context.Player).Target;
        if (target == default || !_context.World.IsAlive(target) || !_context.Has<Transform2>(target))
            return false;
        if (!TryGetPlayerAim(out var aim))
            return false;
        var from = _context.World.Get<Transform2>(_context.Player).Position;
        var to = _context.World.Get<Transform2>(target).Position;
        var radius = _context.Has<Collider>(target) ? _context.World.Get<Collider>(target).Radius : 0f;
        if (!FlightCombatMath.TryRayCircleEntry(from, aim, to, radius, out distance))
            return false;
        return distance > 0.001f;
    }

    public WeaponState WeaponStatus(EntityId entity)
    {
        if (!_context.World.IsAlive(entity) || !_context.Has<WeaponState>(entity))
            throw new InvalidOperationException($"Entity {entity} has no live weapon state.");
        return _context.World.Get<WeaponState>(entity);
    }

    public MobilityAbility MobilityStatus(EntityId entity)
    {
        if (!_context.World.IsAlive(entity) || !_context.Has<MobilityAbility>(entity))
            throw new InvalidOperationException($"Entity {entity} has no live mobility state.");
        return _context.World.Get<MobilityAbility>(entity);
    }

    public TemporaryCombatModifiers TemporaryModifiers(EntityId entity)
    {
        if (!_context.World.IsAlive(entity) || !_context.Has<TemporaryCombatModifiers>(entity))
            throw new InvalidOperationException($"Entity {entity} has no temporary modifier state.");
        return _context.World.Get<TemporaryCombatModifiers>(entity);
    }

    public void InflictDamage(EntityId target, EntityId source, float amount, bool projectile = true)
    {
        if (!_context.World.IsAlive(target) || !_context.World.IsAlive(source))
            return;
        if (!float.IsFinite(amount) || amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (_context.ExternalDamageCount >= _context.ExternalDamage.Length)
            throw new InvalidOperationException("External damage exceeded the deterministic per-tick bound.");
        _context.ExternalDamage[_context.ExternalDamageCount++] = new FlightCombatContext.DamageRequest(target, source, amount, projectile);
    }

    public void QueueAreaDamage(EntityId source, Vector2 center, float radius, float damage, Faction targetFaction)
    {
        if (source == default || !_context.World.IsAlive(source))
            return;
        _context.QueueAreaDamage(source, center, radius, damage, targetFaction);
    }

    /// <summary>Spawns a player-faction bolt from an arbitrary world origin (scout drone assistant).</summary>
    public EntityId SpawnScoutProjectile(Vector2 origin, Vector2 direction, float damage, float speed, float range)
    {
        EnsurePlayer();
        var aim = direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var entity = _context.SpawnProjectile(
            _context.Player,
            aim,
            damage,
            speed,
            range,
            Faction.Player,
            pierces: 0,
            missile: false,
            target: default,
            turnDegrees: 0,
            originOverride: origin);
        _context.AddEvent(CombatEvent.Create(
            CombatEventKind.WeaponFired,
            Tick,
            _context.Player,
            default,
            new ContentId("MOD_UTILITY_DRONE"),
            origin,
            amount: 1));
        return entity;
    }

    public ulong Step()
    {
        _context.ClearTickBuffers();
        _scheduler.Tick(_context.World, _context.Tick);
        _context.Tick++;
        return LastStateHash;
    }

    private void EnsurePlayer()
    {
        if (_context.Player == default || !_context.World.IsAlive(_context.Player))
            throw new InvalidOperationException("A live player is required.");
    }
}
