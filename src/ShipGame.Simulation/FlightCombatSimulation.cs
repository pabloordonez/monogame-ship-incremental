using System.Collections.ObjectModel;
using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public sealed class FlightCombatSimulation
{
    private const uint PlayerLayer = 1;
    private const uint HostileLayer = 2;
    private const uint ProjectileLayer = 4;
    private const uint ObstacleLayer = 8;
    private const uint MineLayer = 16;
    private const int GridWidth = 128;
    private const int GridCells = GridWidth * GridWidth;
    private const float GridCellSize = 64;
    private const float GridOrigin = -4096;

    private readonly World _world = new();
    private readonly FlightCombatBehaviorRegistry _registry;
    private readonly RandomStreams _random;
    private readonly SystemScheduler _scheduler = new();
    private readonly FlightCommandFrame[] _commandSlots = new FlightCommandFrame[FlightCombatConstants.CommandSlotCount];
    private readonly bool[] _commandOccupied = new bool[FlightCombatConstants.CommandSlotCount];
    private readonly List<EntityId> _entities = new(FlightCombatConstants.MaximumEntities);
    private readonly List<EntityId> _pendingDestroy = new(FlightCombatConstants.MaximumEntities);
    private readonly List<CombatEvent> _events = new(FlightCombatConstants.MaximumEventsPerTick);
    private readonly ReadOnlyCollection<CombatEvent> _eventView;
    private readonly DamageRequest[] _damage = new DamageRequest[FlightCombatConstants.MaximumDamageRequestsPerTick];
    private readonly DamageRequest[] _externalDamage = new DamageRequest[FlightCombatConstants.MaximumDamageRequestsPerTick];
    private readonly CollisionPair[] _pairs = new CollisionPair[FlightCombatConstants.MaximumDamageRequestsPerTick];
    private readonly int[] _gridHeads = new int[GridCells];
    private readonly int[] _gridNext = new int[FlightCombatConstants.MaximumEntities];
    private readonly List<SpawnAnchor> _anchors = new(64);
    private int _pendingCommandCount;
    private int _damageCount;
    private int _externalDamageCount;
    private int _pairCount;
    private bool _threatEnabled;
    private int _threatIntervalTicks;
    private int _threatCap;
    private bool _eliteSpawned;
    private EntityId _player;

    public FlightCombatSimulation(
        ulong seed,
        FlightCombatBehaviorRegistry? registry = null)
    {
        _registry = registry ?? FlightCombatBehaviorRegistry.CreateMvp();
        _random = new RandomStreams(seed);
        _eventView = _events.AsReadOnly();
        // Schedule is the exact ordered projection of Step(); Step only runs this registration.
        _scheduler.Add(new DelegateSystem("ApplyFlightCombatStructuralChanges", (_, _) => ApplyStructuralChanges()));
        _scheduler.Add(new DelegateSystem("ConsumeFlightCommands", (_, _) => ConsumeCommands()));
        _scheduler.Add(new DelegateSystem("AdvanceCombatTimers", (_, _) => AdvanceTimers()));
        _scheduler.Add(new DelegateSystem("ConsumeTemporaryModifiers", (_, _) => ConsumeTemporaryModifiers()));
        _scheduler.Add(new DelegateSystem("AiAndThreatDecisions", (_, _) => UpdateAiAndThreat()));
        _scheduler.Add(new DelegateSystem("ResolveMobility", (_, _) => ResolveMobility()));
        _scheduler.Add(new DelegateSystem("IntegrateFlightMovement", (_, _) => IntegrateMovement()));
        _scheduler.Add(new DelegateSystem("RebuildCombatSpatialIndex", (_, _) => RebuildSpatialIndex()));
        _scheduler.Add(new DelegateSystem("DetectCombatCollisions", (_, _) => DetectCollisions()));
        _scheduler.Add(new DelegateSystem("ResolveWeapons", (_, _) => ResolveWeapons()));
        _scheduler.Add(new DelegateSystem("ResolveMines", (_, _) => ResolveMines()));
        _scheduler.Add(new DelegateSystem("ResolveOrderedDamage", (_, _) => ResolveOrderedDamage()));
        _scheduler.Add(new DelegateSystem("ResolveCombatDestruction", (_, _) => ResolveDestruction()));
        _scheduler.Add(new DelegateSystem("PublishCombatEventsAndHash", (_, _) => LastStateHash = CalculateHash()));
        Schedule = _scheduler.Order;
    }

    public long Tick { get; private set; }
    public ulong LastStateHash { get; private set; }
    public EntityId Player => _player;
    public IReadOnlyList<CombatEvent> Events => _eventView;
    public IReadOnlyList<string> Schedule { get; }

    public bool Queue(FlightCommandFrame command)
    {
        if (command.TargetTick < Tick ||
            command.TargetTick > Tick + FlightCombatConstants.CommandHorizonTicks)
        {
            AddEvent(CombatEvent.Create(
                CombatEventKind.CommandRejected,
                Tick,
                amount: command.TargetTick,
                detail: command.TargetTick < Tick ? "stale" : "future"));
            LastStateHash = CalculateHash();
            return false;
        }

        var slot = CommandSlot(command.TargetTick);
        if (_commandOccupied[slot] && _commandSlots[slot].TargetTick != command.TargetTick)
            throw new InvalidOperationException("Command slot map collision within the accepted horizon.");
        if (!_commandOccupied[slot])
        {
            _commandOccupied[slot] = true;
            _pendingCommandCount++;
        }
        _commandSlots[slot] = command;
        LastStateHash = CalculateHash();
        return true;
    }

    public EntityId SpawnPlayer(
        Vector2 position,
        ContentId weaponId,
        MobilityBehavior mobility = MobilityBehavior.Dash)
    {
        if (_player != default && _world.IsAlive(_player))
            throw new InvalidOperationException("Only one player is supported.");
        var entity = CreateEntity();
        _player = entity;
        _world.Set(entity, new Transform2(position, 0));
        _world.Set(entity, new Velocity2(Vector2.Zero));
        _world.Set(entity, new Collider(18, PlayerLayer, HostileLayer | ObstacleLayer | MineLayer));
        _world.Set(entity, new FlightStatistics(900, 1_100, mobility == MobilityBehavior.Dash ? 220 : 200));
        _world.Set(entity, new PlayerControlled());
        _world.Set(entity, new ControlIntent(Vector2.Zero, Vector2.UnitX, FlightAction.None, FlightAction.None));
        _world.Set(entity, new Combatant(Faction.Player));
        _world.Set(entity, new Health(100, 100));
        _world.Set(entity, new Shield(60, 60, 12, 180, 180));
        var weapon = _registry.Weapon(weaponId);
        _world.Set(entity, new WeaponMount(weaponId, weapon.Behavior));
        _world.Set(entity, new WeaponState(0, 0, false, default));
        var abilityId = new ContentId(mobility == MobilityBehavior.Dash ? "MOD_ENGINE_VECTOR" : "MOD_ENGINE_BLINK");
        _world.Set(
            entity,
            mobility == MobilityBehavior.Dash
                ? new MobilityAbility(abilityId, mobility, 180, 11, 240, 0, 0, Vector2.Zero)
                : new MobilityAbility(abilityId, mobility, 260, 0, 360, 0, 0, Vector2.Zero));
        _world.Set(entity, DefaultModifiers());
        return entity;
    }

    public EntityId SpawnEnemy(ContentId enemyId, Vector2 position, bool elite = false)
    {
        if (elite && _eliteSpawned)
            throw new InvalidOperationException("MOD_ELITE_PROTOCOL allows only one elite per run.");
        var definition = _registry.Enemy(enemyId);
        var healthMultiplier = elite ? 2.75f : 1;
        var speedMultiplier = elite ? 1.10f : 1;
        var entity = CreateEntity();
        _world.Set(entity, new Transform2(position, 0));
        _world.Set(entity, new Velocity2(Vector2.Zero));
        _world.Set(entity, new Collider(16 * (elite ? 1.35f : 1), HostileLayer, PlayerLayer | ObstacleLayer | ProjectileLayer));
        _world.Set(entity, new FlightStatistics(definition.Speed * 5, definition.Speed * 7, definition.Speed * speedMultiplier));
        _world.Set(entity, new ControlIntent(Vector2.Zero, -Vector2.UnitX, FlightAction.None, FlightAction.None));
        _world.Set(entity, new Combatant(Faction.Hostile));
        _world.Set(entity, new Health(definition.Hull * healthMultiplier, definition.Hull * healthMultiplier));
        _world.Set(entity, new AiBrain(definition.Behavior, 0, 0, 0));
        _world.Set(entity, new ThreatValue(definition.Behavior == EnemyBehavior.Gunship ? 2 : 1));
        _world.Set(entity, new Target(_player));
        _world.Set(entity, new WeaponState(definition.CadenceTicks, 0, false, default));
        _world.Set(entity, new WeaponMount(enemyId, WeaponBehavior.Pulse));
        _world.Set(entity, DefaultModifiers());
        if (elite)
        {
            _eliteSpawned = true;
            _world.Set(entity, new Elite(1.35f));
            AddEvent(CombatEvent.Create(
                CombatEventKind.EliteActivated,
                Tick,
                entity,
                contentId: new ContentId("MOD_ELITE_PROTOCOL"),
                position: position));
        }
        AddEvent(CombatEvent.Create(
            CombatEventKind.EnemySpawned,
            Tick,
            entity,
            contentId: enemyId,
            amount: elite ? 1 : 0));
        return entity;
    }

    public EntityId SpawnObstacle(Vector2 position, float radius)
    {
        if (!float.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var entity = CreateEntity();
        _world.Set(entity, new Transform2(position, 0));
        _world.Set(entity, new Collider(radius, ObstacleLayer, PlayerLayer | HostileLayer | ProjectileLayer));
        _world.Set(entity, new Combatant(Faction.Neutral));
        return entity;
    }

    public void AddSpawnAnchor(Vector2 position, bool outsideCamera = true)
    {
        if (_anchors.Count >= 64)
            throw new InvalidOperationException("Threat anchors are bounded to 64.");
        _anchors.Add(new SpawnAnchor(position, outsideCamera));
    }

    public void ConfigureThreatDirector(int intervalTicks, int activeCap)
    {
        if (intervalTicks <= 0 || activeCap is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(intervalTicks));
        _threatEnabled = true;
        _threatIntervalTicks = intervalTicks;
        _threatCap = activeCap;
    }

    public void GrantTemporaryModifiers(TemporaryCombatModifiers modifiers)
    {
        EnsurePlayer();
        ValidateModifiers(modifiers);
        _world.Set(_player, new PendingTemporaryModifier(modifiers));
    }

    public void ClearTemporaryModifiers()
    {
        EnsurePlayer();
        _world.Set(_player, DefaultModifiers());
        if (_world.Store<PendingTemporaryModifier>().Has(_player))
            _world.Remove<PendingTemporaryModifier>(_player);
    }

    public CombatSnapshot Snapshot(EntityId entity)
    {
        if (!_world.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is stale or dead.");
        var transform = _world.Store<Transform2>().Has(entity)
            ? _world.Store<Transform2>().Read(entity)
            : default;
        var faction = _world.Store<Combatant>().Has(entity)
            ? _world.Store<Combatant>().Read(entity).Faction
            : Faction.Neutral;
        var hull = _world.Store<Health>().Has(entity) ? _world.Store<Health>().Read(entity).Current : 0;
        var shield = _world.Store<Shield>().Has(entity) ? _world.Store<Shield>().Read(entity).Current : 0;
        return new CombatSnapshot(
            Tick,
            entity,
            transform.Position,
            transform.Rotation,
            faction,
            hull,
            shield,
            _world.Store<Destroyed>().Has(entity));
    }

    /// <summary>Ordered live combat snapshots for presentation (P5).</summary>
    public void CollectSnapshots(List<CombatSnapshot> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (!_world.IsAlive(entity) || !Has<Transform2>(entity))
                continue;
            into.Add(Snapshot(entity));
        }
    }

    /// <summary>Presentation-oriented entity list with render kind (skips destroyed).</summary>
    public void CollectRenderItems(List<CombatRenderItem> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (!_world.IsAlive(entity) || !Has<Transform2>(entity) || Has<Destroyed>(entity))
                continue;
            var transform = _world.Get<Transform2>(entity);
            var faction = Has<Combatant>(entity) ? _world.Get<Combatant>(entity).Faction : Faction.Neutral;
            var kind = CombatRenderKind.Obstacle;
            if (Has<Projectile>(entity))
                kind = CombatRenderKind.Projectile;
            else if (Has<Mine>(entity))
                kind = CombatRenderKind.Mine;
            else if (faction == Faction.Player && Has<PlayerControlled>(entity))
                kind = CombatRenderKind.PlayerShip;
            else if (faction == Faction.Hostile)
                kind = CombatRenderKind.EnemyShip;
            else
                continue; // skip static obstacles; asteroids are drawn from world-run data

            into.Add(new(
                entity,
                transform.Position,
                transform.Rotation,
                faction,
                kind,
                Has<Elite>(entity),
                Has<Health>(entity) ? _world.Get<Health>(entity).Current : 0,
                Has<Shield>(entity) ? _world.Get<Shield>(entity).Current : 0));
        }
    }

    public bool IsElite(EntityId entity) =>
        entity != default && _world.IsAlive(entity) && Has<Elite>(entity);

    public bool TryGetPlayerAim(out Vector2 aim)
    {
        aim = Vector2.UnitX;
        if (_player == default || !_world.IsAlive(_player) || !Has<ControlIntent>(_player))
            return false;
        aim = _world.Get<ControlIntent>(_player).Aim;
        if (aim.LengthSquared() < 0.0001f)
            aim = Vector2.UnitX;
        else
            aim = Vector2.Normalize(aim);
        return true;
    }

    public WeaponState WeaponStatus(EntityId entity)
    {
        if (!_world.IsAlive(entity) || !Has<WeaponState>(entity))
            throw new InvalidOperationException($"Entity {entity} has no live weapon state.");
        return _world.Get<WeaponState>(entity);
    }

    public MobilityAbility MobilityStatus(EntityId entity)
    {
        if (!_world.IsAlive(entity) || !Has<MobilityAbility>(entity))
            throw new InvalidOperationException($"Entity {entity} has no live mobility state.");
        return _world.Get<MobilityAbility>(entity);
    }

    public TemporaryCombatModifiers TemporaryModifiers(EntityId entity)
    {
        if (!_world.IsAlive(entity) || !Has<TemporaryCombatModifiers>(entity))
            throw new InvalidOperationException($"Entity {entity} has no temporary modifier state.");
        return _world.Get<TemporaryCombatModifiers>(entity);
    }

    public void InflictDamage(EntityId target, EntityId source, float amount, bool projectile = true)
    {
        if (!_world.IsAlive(target) || !_world.IsAlive(source))
            return;
        if (!float.IsFinite(amount) || amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (_externalDamageCount >= _externalDamage.Length)
            throw new InvalidOperationException("External damage exceeded the deterministic per-tick bound.");
        _externalDamage[_externalDamageCount++] = new DamageRequest(target, source, amount, projectile);
    }

    public ulong Step()
    {
        _events.Clear();
        _damageCount = 0;
        for (var i = 0; i < _externalDamageCount; i++)
            _damage[_damageCount++] = _externalDamage[i];
        _externalDamageCount = 0;
        _scheduler.Tick(_world, Tick);
        Tick++;
        return LastStateHash;
    }

    private void ApplyStructuralChanges()
    {
        for (var i = 0; i < _pendingDestroy.Count; i++)
        {
            var entity = _pendingDestroy[i];
            if (!_world.IsAlive(entity))
                continue;
            _world.Destroy(entity);
            _entities.Remove(entity);
            if (_player == entity)
                _player = default;
        }
        _pendingDestroy.Clear();
    }

    private void ConsumeCommands()
    {
        if (_player == default || !_world.IsAlive(_player))
        {
            TryTakeCommand(Tick, out _);
            return;
        }
        ref var intent = ref _world.Get<ControlIntent>(_player);
        var previous = intent.Actions;
        var command = TryTakeCommand(Tick, out var found)
            ? found
            : FlightCommandFrame.Neutral(Tick);
        var aim = command.Aim;
        if (aim.LengthSquared() <= 0.0001f)
            aim = intent.Aim;
        intent = new ControlIntent(command.Move, aim, command.Actions, previous);
    }

    private bool TryTakeCommand(long tick, out FlightCommandFrame command)
    {
        var slot = CommandSlot(tick);
        if (_commandOccupied[slot] && _commandSlots[slot].TargetTick == tick)
        {
            command = _commandSlots[slot];
            _commandOccupied[slot] = false;
            _pendingCommandCount--;
            return true;
        }
        command = default;
        return false;
    }

    private static int CommandSlot(long tick) =>
        (int)(tick % FlightCombatConstants.CommandSlotCount);

    private void AdvanceTimers()
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (Has<Destroyed>(entity))
                continue;
            if (Has<Shield>(entity))
            {
                ref var shield = ref _world.Get<Shield>(entity);
                var ticks = shield.TicksSinceDamage + 1;
                var current = shield.Current;
                if (ticks >= shield.RechargeDelayTicks && current < shield.Maximum)
                    current = MathF.Min(shield.Maximum, current + shield.RechargePerSecond * FlightCombatConstants.TickSeconds);
                shield = shield with { Current = current, TicksSinceDamage = ticks };
            }
            if (Has<WeaponState>(entity))
            {
                ref var state = ref _world.Get<WeaponState>(entity);
                state = state with { CooldownTicks = Math.Max(0, state.CooldownTicks - 1) };
            }
            if (Has<MobilityAbility>(entity))
            {
                ref var ability = ref _world.Get<MobilityAbility>(entity);
                ability = ability with
                {
                    CooldownRemaining = Math.Max(0, ability.CooldownRemaining - 1),
                    ActiveTicksRemaining = Math.Max(0, ability.ActiveTicksRemaining - 1)
                };
            }
            if (Has<Invulnerability>(entity))
            {
                ref var value = ref _world.Get<Invulnerability>(entity);
                value = new Invulnerability(Math.Max(0, value.TicksRemaining - 1));
            }
            if (Has<Projectile>(entity))
            {
                ref var projectile = ref _world.Get<Projectile>(entity);
                projectile = projectile with { LifetimeTicks = projectile.LifetimeTicks - 1 };
                if (projectile.LifetimeTicks <= 0)
                    MarkDestroyed(entity, default);
            }
            if (Has<Mine>(entity))
            {
                ref var mine = ref _world.Get<Mine>(entity);
                mine = mine with
                {
                    ArmTicks = Math.Max(0, mine.ArmTicks - 1),
                    LifetimeTicks = mine.LifetimeTicks - 1
                };
                if (mine.LifetimeTicks <= 0)
                    MarkDestroyed(entity, default);
            }
        }
    }

    private void ConsumeTemporaryModifiers()
    {
        if (_player == default || !Has<PendingTemporaryModifier>(_player))
            return;
        var grant = _world.Get<PendingTemporaryModifier>(_player).Value;
        var current = _world.Get<TemporaryCombatModifiers>(_player);
        _world.Get<TemporaryCombatModifiers>(_player) = new TemporaryCombatModifiers(
            current.DamageMultiplier * grant.DamageMultiplier,
            current.FireRateMultiplier * grant.FireRateMultiplier,
            current.SpeedMultiplier * grant.SpeedMultiplier,
            current.MobilityCooldownMultiplier * grant.MobilityCooldownMultiplier,
            Math.Clamp(current.ExtraProjectiles + grant.ExtraProjectiles, 0, 4),
            MathF.Max(current.ExtraProjectileDamageMultiplier, grant.ExtraProjectileDamageMultiplier),
            Math.Clamp(current.PierceCount + grant.PierceCount, 0, 4),
            current.ShockTransit || grant.ShockTransit);
        _world.Remove<PendingTemporaryModifier>(_player);
    }

    private void UpdateAiAndThreat()
    {
        var count = _entities.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = _entities[i];
            if (!Has<AiBrain>(entity) || Has<Destroyed>(entity))
                continue;
            if (_player == default || !IsTargetable(_player))
            {
                ref var noTargetIntent = ref _world.Get<ControlIntent>(entity);
                noTargetIntent = noTargetIntent with { Move = Vector2.Zero, Actions = FlightAction.None };
                continue;
            }
            var definition = _registry.Enemy(_world.Get<WeaponMount>(entity).BehaviorId);
            var transform = _world.Get<Transform2>(entity);
            var targetTransform = _world.Get<Transform2>(_player);
            var delta = targetTransform.Position - transform.Position;
            var distance = delta.Length();
            var direction = NormalizeOr(delta, Vector2.UnitX);
            ref var brain = ref _world.Get<AiBrain>(entity);
            ref var intent = ref _world.Get<ControlIntent>(entity);
            ref var weapon = ref _world.Get<WeaponState>(entity);
            var move = Vector2.Zero;
            var actions = FlightAction.None;

            switch (brain.Behavior)
            {
                case EnemyBehavior.Interceptor:
                    if (brain.StateTicks > 0)
                    {
                        move = -direction;
                        brain = brain with { StateTicks = brain.StateTicks - 1 };
                    }
                    else
                    {
                        move = distance > definition.PreferredRange ? direction : Perpendicular(direction);
                        if (weapon.CooldownTicks == 0)
                        {
                            brain = brain with { BurstShotsRemaining = 3, StateTicks = 60 };
                            weapon = weapon with { CooldownTicks = definition.CadenceTicks };
                        }
                    }
                    if (brain.BurstShotsRemaining > 0 && Tick % 9 == 0)
                    {
                        SpawnHostileProjectile(entity, direction, definition.Damage, 520);
                        brain = brain with { BurstShotsRemaining = brain.BurstShotsRemaining - 1 };
                    }
                    break;
                case EnemyBehavior.Gunship:
                    move = distance > definition.PreferredRange + 20
                        ? direction
                        : distance < definition.PreferredRange - 20 ? -direction : Perpendicular(direction);
                    if (weapon.CooldownTicks == 0)
                    {
                        SpawnHostileProjectile(entity, direction, EffectiveEnemyDamage(entity, definition.Damage), 420);
                        weapon = weapon with { CooldownTicks = EffectiveEnemyCadence(entity, definition.CadenceTicks) };
                    }
                    break;
                case EnemyBehavior.Sapper:
                    move = distance > definition.PreferredRange ? direction : -direction;
                    if (weapon.CooldownTicks == 0 && brain.ActiveMines < 2)
                    {
                        SpawnMine(entity, transform.Position, EffectiveEnemyDamage(entity, definition.Damage));
                        brain = brain with { ActiveMines = brain.ActiveMines + 1 };
                        weapon = weapon with { CooldownTicks = EffectiveEnemyCadence(entity, definition.CadenceTicks) };
                    }
                    break;
            }
            intent = new ControlIntent(move, direction, actions, intent.Actions);
        }

        if (!_threatEnabled || Tick == 0 || Tick % _threatIntervalTicks != 0 || _player == default)
            return;
        var hostileCount = 0;
        for (var i = 0; i < _entities.Count; i++)
            if (Has<AiBrain>(_entities[i]) && !Has<Destroyed>(_entities[i]))
                hostileCount++;
        if (hostileCount >= _threatCap)
            return;
        Span<int> valid = stackalloc int[64];
        var validCount = 0;
        var playerPosition = _world.Get<Transform2>(_player).Position;
        for (var i = 0; i < _anchors.Count; i++)
            if (_anchors[i].OutsideCamera && Vector2.DistanceSquared(_anchors[i].Position, playerPosition) >= 450 * 450)
                valid[validCount++] = i;
        if (validCount == 0)
            return;
        var rng = _random.Get(RngStream.Encounter);
        var anchor = _anchors[valid[(int)(rng.NextUInt() % (uint)validCount)]];
        var enemy = (rng.NextUInt() % 3) switch
        {
            0 => new ContentId("ENM_INTERCEPTOR"),
            1 => new ContentId("ENM_GUNSHIP"),
            _ => new ContentId("ENM_SAPPER")
        };
        SpawnEnemy(enemy, anchor.Position);
    }

    private void ResolveMobility()
    {
        if (_player == default || !Has<MobilityAbility>(_player) || Has<Destroyed>(_player))
            return;
        var intent = _world.Get<ControlIntent>(_player);
        if ((intent.Actions & FlightAction.Mobility) == 0 ||
            (intent.PreviousActions & FlightAction.Mobility) != 0)
            return;
        ref var ability = ref _world.Get<MobilityAbility>(_player);
        if (ability.CooldownRemaining > 0)
        {
            AddEvent(CombatEvent.Create(
                CombatEventKind.AbilityRejected,
                Tick,
                _player,
                contentId: ability.BehaviorId,
                detail: "cooldown"));
            return;
        }
        var direction = NormalizeOr(intent.Move, NormalizeOr(intent.Aim, Vector2.UnitX));
        var start = _world.Get<Transform2>(_player).Position;
        var destination = ShortenAgainstObstacles(_player, start, start + direction * ability.Distance);
        ref var transform = ref _world.Get<Transform2>(_player);
        transform = transform with { Position = destination, Rotation = MathF.Atan2(direction.Y, direction.X) };
        var modifiers = _world.Get<TemporaryCombatModifiers>(_player);
        var cooldown = Math.Max(1, (int)MathF.Round(ability.CooldownTicks * modifiers.MobilityCooldownMultiplier));
        ability = ability with
        {
            CooldownRemaining = cooldown,
            ActiveTicksRemaining = ability.Behavior == MobilityBehavior.Dash ? ability.DurationTicks : 0,
            Direction = direction
        };
        if (ability.Behavior == MobilityBehavior.Dash)
            _world.Set(_player, new Invulnerability(ability.DurationTicks));
        AddEvent(CombatEvent.Create(
            CombatEventKind.AbilityActivated,
            Tick,
            _player,
            contentId: ability.BehaviorId,
            position: destination));
        if (modifiers.ShockTransit)
            QueueAreaDamage(_player, destination, 90, 20 * modifiers.DamageMultiplier, Faction.Hostile);
    }

    private void IntegrateMovement()
    {
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            if (!Has<Velocity2>(entity) || !Has<Transform2>(entity) || Has<Destroyed>(entity))
                continue;
            ref var velocity = ref _world.Get<Velocity2>(entity);
            ref var transform = ref _world.Get<Transform2>(entity);
            if (Has<FlightStatistics>(entity) && Has<ControlIntent>(entity))
            {
                var statistics = _world.Get<FlightStatistics>(entity);
                var intent = _world.Get<ControlIntent>(entity);
                var modifiers = Has<TemporaryCombatModifiers>(entity)
                    ? _world.Get<TemporaryCombatModifiers>(entity)
                    : DefaultModifiers();
                var maxSpeed = statistics.MaximumSpeed * modifiers.SpeedMultiplier;
                var target = intent.Move * maxSpeed;
                var rate = intent.Move.LengthSquared() > 0.0001f ? statistics.Acceleration : statistics.Braking;
                velocity = new Velocity2(MoveTowards(velocity.Value, target, rate * FlightCombatConstants.TickSeconds));
                if (velocity.Value.LengthSquared() > maxSpeed * maxSpeed)
                    velocity = new Velocity2(Vector2.Normalize(velocity.Value) * maxSpeed);
                var facing = intent.Aim.LengthSquared() > 0.0001f
                    ? MathF.Atan2(intent.Aim.Y, intent.Aim.X)
                    : transform.Rotation;
                transform = transform with { Rotation = facing };
            }
            if (Has<Homing>(entity))
                GuideProjectile(entity);
            transform = transform with { Position = transform.Position + velocity.Value * FlightCombatConstants.TickSeconds };
        }
    }

    private void RebuildSpatialIndex()
    {
        Array.Fill(_gridHeads, -1);
        Array.Fill(_gridNext, -1);
        for (var i = _entities.Count - 1; i >= 0; i--)
        {
            var entity = _entities[i];
            if (!Has<Transform2>(entity) || !Has<Collider>(entity) || Has<Destroyed>(entity))
                continue;
            var cell = Cell(_world.Get<Transform2>(entity).Position);
            _gridNext[i] = _gridHeads[cell];
            _gridHeads[cell] = i;
        }
    }

    private void DetectCollisions()
    {
        _pairCount = 0;
        for (var firstIndex = 0; firstIndex < _entities.Count; firstIndex++)
        {
            var first = _entities[firstIndex];
            if (!Has<Transform2>(first) || !Has<Collider>(first) || Has<Destroyed>(first))
                continue;
            var firstTransform = _world.Get<Transform2>(first);
            var cellX = CellCoordinate(firstTransform.Position.X);
            var cellY = CellCoordinate(firstTransform.Position.Y);
            for (var y = Math.Max(0, cellY - 1); y <= Math.Min(GridWidth - 1, cellY + 1); y++)
            for (var x = Math.Max(0, cellX - 1); x <= Math.Min(GridWidth - 1, cellX + 1); x++)
            for (var secondIndex = _gridHeads[y * GridWidth + x]; secondIndex >= 0; secondIndex = _gridNext[secondIndex])
            {
                if (secondIndex <= firstIndex)
                    continue;
                var second = _entities[secondIndex];
                var firstCollider = _world.Get<Collider>(first);
                var secondCollider = _world.Get<Collider>(second);
                if ((firstCollider.Mask & secondCollider.Layer) == 0 &&
                    (secondCollider.Mask & firstCollider.Layer) == 0)
                    continue;
                var secondTransform = _world.Get<Transform2>(second);
                var radius = firstCollider.Radius + secondCollider.Radius;
                if (Vector2.DistanceSquared(firstTransform.Position, secondTransform.Position) > radius * radius)
                    continue;
                if (_pairCount >= _pairs.Length)
                    throw new InvalidOperationException("Collision work exceeded the deterministic per-tick bound.");
                _pairs[_pairCount++] = new CollisionPair(first, second);
            }
        }
        Array.Sort(_pairs, 0, _pairCount, CollisionPairComparer.Instance);
        for (var i = 0; i < _pairCount; i++)
            ResolveCollision(_pairs[i]);
    }

    private void ResolveCollision(CollisionPair pair)
    {
        if (!IsTargetable(pair.First) || !IsTargetable(pair.Second))
            return;
        AddEvent(CombatEvent.Create(CombatEventKind.CollisionDetected, Tick, pair.First, pair.Second));
        var firstProjectile = Has<DamageSource>(pair.First) && Has<Projectile>(pair.First);
        var secondProjectile = Has<DamageSource>(pair.Second) && Has<Projectile>(pair.Second);
        if (firstProjectile)
            ResolveProjectileContact(pair.First, pair.Second);
        if (secondProjectile)
            ResolveProjectileContact(pair.Second, pair.First);
        if (Has<ContactDamage>(pair.First))
            QueueDamage(pair.Second, pair.First, _world.Get<ContactDamage>(pair.First).Damage, false);
        if (Has<ContactDamage>(pair.Second))
            QueueDamage(pair.First, pair.Second, _world.Get<ContactDamage>(pair.Second).Damage, false);
        if (!firstProjectile && !secondProjectile)
            SeparateBlockingPair(pair.First, pair.Second);
    }

    private void ResolveWeapons()
    {
        var count = _entities.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = _entities[i];
            if (!Has<PlayerControlled>(entity) || !Has<WeaponMount>(entity) || Has<Destroyed>(entity))
                continue;
            var intent = _world.Get<ControlIntent>(entity);
            var firing = (intent.Actions & FlightAction.Fire) != 0;
            ref var state = ref _world.Get<WeaponState>(entity);
            var definition = _registry.Weapon(_world.Get<WeaponMount>(entity).BehaviorId);
            var modifiers = _world.Get<TemporaryCombatModifiers>(entity);

            if (definition.Behavior == WeaponBehavior.Beam)
            {
                if (!firing)
                {
                    var heat = MathF.Max(0, state.Heat - definition.CoolPerTick);
                    state = state with { Heat = heat, HeatLocked = state.HeatLocked && heat > 0 };
                    continue;
                }
                if (state.HeatLocked)
                    continue;
                var target = FindTargetInCone(entity, intent.Aim, definition.Range, 4);
                if (target != default)
                {
                    QueueDamage(
                        target,
                        entity,
                        definition.Damage * FlightCombatConstants.TickSeconds * modifiers.DamageMultiplier * modifiers.FireRateMultiplier,
                        false);
                    AddEvent(CombatEvent.Create(
                        CombatEventKind.WeaponFired,
                        Tick,
                        entity,
                        target,
                        definition.Id));
                }
                var nextHeat = MathF.Min(180, state.Heat + definition.HeatPerTick);
                state = state with { Heat = nextHeat, HeatLocked = nextHeat >= 180, Target = target };
                continue;
            }

            if (!firing || state.CooldownTicks > 0)
                continue;
            var cadence = Math.Max(1, (int)MathF.Round(definition.CadenceTicks / modifiers.FireRateMultiplier));
            if (definition.Behavior == WeaponBehavior.Pulse)
            {
                SpawnPlayerProjectiles(entity, intent.Aim, definition, modifiers, default);
                state = state with { CooldownTicks = cadence };
                continue;
            }
            var lockTarget = FindTargetInCone(entity, intent.Aim, definition.Range, definition.LockConeDegrees);
            if (lockTarget == default)
                continue;
            SpawnPlayerProjectiles(entity, intent.Aim, definition, modifiers, lockTarget);
            state = state with { CooldownTicks = cadence, Target = lockTarget };
        }
    }

    private void ResolveMines()
    {
        var count = _entities.Count;
        for (var i = 0; i < count; i++)
        {
            var entity = _entities[i];
            if (!Has<Mine>(entity) || Has<Destroyed>(entity))
                continue;
            var mine = _world.Get<Mine>(entity);
            if (mine.ArmTicks != 0 || _player == default || !IsTargetable(_player))
                continue;
            var position = _world.Get<Transform2>(entity).Position;
            var playerPosition = _world.Get<Transform2>(_player).Position;
            if (Vector2.DistanceSquared(position, playerPosition) > mine.Radius * mine.Radius)
                continue;
            QueueDamage(_player, mine.Owner, mine.Damage, false);
            MarkDestroyed(entity, mine.Owner);
            if (_world.IsAlive(mine.Owner) && Has<AiBrain>(mine.Owner))
            {
                ref var brain = ref _world.Get<AiBrain>(mine.Owner);
                brain = brain with { ActiveMines = Math.Max(0, brain.ActiveMines - 1) };
            }
        }
    }

    private void ResolveOrderedDamage()
    {
        Array.Sort(_damage, 0, _damageCount, DamageRequestComparer.Instance);
        var playerHullDamaged = false;
        for (var i = 0; i < _damageCount; i++)
        {
            var request = _damage[i];
            if (!IsTargetable(request.Target) || !Has<Health>(request.Target) ||
                Has<Invulnerability>(request.Target) && _world.Get<Invulnerability>(request.Target).TicksRemaining > 0)
                continue;
            var remaining = request.Amount;
            if (Has<Shield>(request.Target))
            {
                ref var shield = ref _world.Get<Shield>(request.Target);
                var absorbed = MathF.Min(shield.Current, remaining);
                if (absorbed > 0)
                {
                    var before = shield.Current;
                    shield = shield with { Current = before - absorbed, TicksSinceDamage = 0 };
                    remaining -= absorbed;
                    AddEvent(CombatEvent.Create(
                        CombatEventKind.ShieldDamaged,
                        Tick,
                        request.Target,
                        request.Source,
                        amount: absorbed,
                        remaining: shield.Current));
                    if (before > 0 && shield.Current <= 0)
                        AddEvent(CombatEvent.Create(CombatEventKind.ShieldDepleted, Tick, request.Target, request.Source));
                }
            }
            if (remaining <= 0)
                continue;
            ref var health = ref _world.Get<Health>(request.Target);
            var applied = MathF.Min(health.Current, remaining);
            health = health with { Current = health.Current - applied };
            AddEvent(CombatEvent.Create(
                CombatEventKind.HullDamaged,
                Tick,
                request.Target,
                request.Source,
                amount: applied,
                remaining: health.Current));
            if (request.Target == _player && applied > 0)
                playerHullDamaged = true;
            if (health.Current <= 0)
                MarkDestroyed(request.Target, request.Source);
        }
        if (playerHullDamaged && _player != default && _world.IsAlive(_player))
            _world.Set(_player, new Invulnerability(21));
    }

    private void ResolveDestruction()
    {
        // Destruction is marked in stable damage order and physically removed next tick.
    }

    private void SpawnPlayerProjectiles(
        EntityId source,
        Vector2 aim,
        WeaponDefinition definition,
        TemporaryCombatModifiers modifiers,
        EntityId target)
    {
        var direction = NormalizeOr(aim, Vector2.UnitX);
        var count = definition.BurstCount + modifiers.ExtraProjectiles;
        for (var index = 0; index < count; index++)
        {
            var angle = count == 1 ? 0 : (index - (count - 1) * 0.5f) * 0.08f;
            var shotDirection = Rotate(direction, angle);
            var multiplier = index < definition.BurstCount ? 1 : modifiers.ExtraProjectileDamageMultiplier;
            SpawnProjectile(
                source,
                shotDirection,
                definition.Damage * modifiers.DamageMultiplier * multiplier,
                definition.ProjectileSpeed,
                definition.Range,
                Faction.Player,
                modifiers.PierceCount,
                definition.Behavior == WeaponBehavior.Seeker,
                target,
                definition.TurnDegreesPerSecond);
        }
        AddEvent(CombatEvent.Create(
            CombatEventKind.WeaponFired,
            Tick,
            source,
            target,
            definition.Id,
            amount: count));
    }

    private void SpawnHostileProjectile(EntityId source, Vector2 direction, float damage, float speed) =>
        SpawnProjectile(source, direction, damage, speed, 700, Faction.Hostile, 0, false, default, 0);

    private EntityId SpawnProjectile(
        EntityId source,
        Vector2 direction,
        float damage,
        float speed,
        float range,
        Faction faction,
        int pierces,
        bool missile,
        EntityId target,
        float turnDegrees)
    {
        var entity = CreateEntity();
        var sourceTransform = _world.Get<Transform2>(source);
        var radius = missile ? 5 : 3;
        var normalized = NormalizeOr(direction, Vector2.UnitX);
        _world.Set(entity, new Transform2(sourceTransform.Position + normalized * (radius + 20), MathF.Atan2(normalized.Y, normalized.X)));
        _world.Set(entity, new Velocity2(normalized * speed));
        _world.Set(entity, new Collider(radius, ProjectileLayer, faction == Faction.Player ? HostileLayer | ObstacleLayer : PlayerLayer | ObstacleLayer, false));
        _world.Set(entity, new Combatant(faction));
        _world.Set(entity, new DamageSource(source, faction, damage, true));
        _world.Set(entity, new Projectile(Math.Max(1, (int)MathF.Ceiling(range / speed * FlightCombatConstants.TickRate)), pierces, missile));
        if (missile)
            _world.Set(entity, new Homing(target, speed, turnDegrees * MathF.PI / 180f));
        return entity;
    }

    private void SpawnMine(EntityId owner, Vector2 position, float damage)
    {
        var entity = CreateEntity();
        _world.Set(entity, new Transform2(position, 0));
        _world.Set(entity, new Collider(8, MineLayer, PlayerLayer, false));
        _world.Set(entity, new Combatant(Faction.Hostile));
        _world.Set(entity, new Mine(60, 480, 75, damage, owner));
        AddEvent(CombatEvent.Create(
            CombatEventKind.MineTelegraphed,
            Tick,
            entity,
            owner,
            position: position,
            amount: 75));
    }

    private void ResolveProjectileContact(EntityId projectileEntity, EntityId target)
    {
        if (!Has<DamageSource>(projectileEntity) || !Has<Projectile>(projectileEntity) ||
            !Has<Combatant>(target))
            return;
        var source = _world.Get<DamageSource>(projectileEntity);
        var targetFaction = _world.Get<Combatant>(target).Faction;
        if (target == source.Owner || targetFaction == source.Faction || targetFaction == Faction.Neutral)
        {
            if (Has<Collider>(target) && _world.Get<Collider>(target).Layer == ObstacleLayer)
                MarkDestroyed(projectileEntity, source.Owner);
            return;
        }
        QueueDamage(target, source.Owner, source.Damage, source.Projectile);
        ref var projectile = ref _world.Get<Projectile>(projectileEntity);
        if (projectile.RemainingPierces > 0)
            projectile = projectile with { RemainingPierces = projectile.RemainingPierces - 1 };
        else
            MarkDestroyed(projectileEntity, source.Owner);
    }

    private void SeparateBlockingPair(EntityId first, EntityId second)
    {
        var firstCollider = _world.Get<Collider>(first);
        var secondCollider = _world.Get<Collider>(second);
        if (!firstCollider.BlocksMovement || !secondCollider.BlocksMovement)
            return;
        var firstMovable = Has<Velocity2>(first);
        var secondMovable = Has<Velocity2>(second);
        if (!firstMovable && !secondMovable)
            return;
        ref var firstTransform = ref _world.Get<Transform2>(first);
        ref var secondTransform = ref _world.Get<Transform2>(second);
        var delta = secondTransform.Position - firstTransform.Position;
        var distance = delta.Length();
        var normal = distance > 0.0001f ? delta / distance : Vector2.UnitX;
        var overlap = firstCollider.Radius + secondCollider.Radius - distance;
        if (overlap <= 0)
            return;
        if (firstMovable && secondMovable)
        {
            firstTransform = firstTransform with { Position = firstTransform.Position - normal * overlap * 0.5f };
            secondTransform = secondTransform with { Position = secondTransform.Position + normal * overlap * 0.5f };
        }
        else if (firstMovable)
            firstTransform = firstTransform with { Position = firstTransform.Position - normal * overlap };
        else
            secondTransform = secondTransform with { Position = secondTransform.Position + normal * overlap };
    }

    private void GuideProjectile(EntityId entity)
    {
        ref var homing = ref _world.Get<Homing>(entity);
        if (!IsTargetable(homing.Target))
        {
            homing = homing with { Target = default };
            return;
        }
        ref var velocity = ref _world.Get<Velocity2>(entity);
        var position = _world.Get<Transform2>(entity).Position;
        var desired = NormalizeOr(_world.Get<Transform2>(homing.Target).Position - position, NormalizeOr(velocity.Value, Vector2.UnitX));
        var currentAngle = MathF.Atan2(velocity.Value.Y, velocity.Value.X);
        var desiredAngle = MathF.Atan2(desired.Y, desired.X);
        var difference = WrapAngle(desiredAngle - currentAngle);
        var maxTurn = homing.TurnRadiansPerSecond * FlightCombatConstants.TickSeconds;
        var angle = currentAngle + Math.Clamp(difference, -maxTurn, maxTurn);
        velocity = new Velocity2(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * homing.Speed);
    }

    private EntityId FindTargetInCone(EntityId source, Vector2 aim, float range, float halfConeDegrees)
    {
        var origin = _world.Get<Transform2>(source).Position;
        var direction = NormalizeOr(aim, Vector2.UnitX);
        var minimumDot = halfConeDegrees <= 0 ? 0.999f : MathF.Cos(halfConeDegrees * MathF.PI / 180f);
        var best = default(EntityId);
        var bestDistanceSquared = range * range;
        for (var i = 0; i < _entities.Count; i++)
        {
            var candidate = _entities[i];
            if (candidate == source || !IsTargetable(candidate) || !Has<Combatant>(candidate))
                continue;
            if (_world.Get<Combatant>(candidate).Faction != Faction.Hostile)
                continue;
            var delta = _world.Get<Transform2>(candidate).Position - origin;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared > bestDistanceSquared || distanceSquared <= 0.0001f)
                continue;
            if (Vector2.Dot(direction, Vector2.Normalize(delta)) < minimumDot)
                continue;
            best = candidate;
            bestDistanceSquared = distanceSquared;
        }
        return best;
    }

    private Vector2 ShortenAgainstObstacles(EntityId mover, Vector2 start, Vector2 requested)
    {
        var direction = requested - start;
        var distance = direction.Length();
        if (distance <= 0.0001f)
            return start;
        direction /= distance;
        var radius = _world.Get<Collider>(mover).Radius;
        var allowed = distance;
        for (var i = 0; i < _entities.Count; i++)
        {
            var obstacle = _entities[i];
            if (!Has<Collider>(obstacle) || !Has<Transform2>(obstacle) ||
                _world.Get<Collider>(obstacle).Layer != ObstacleLayer)
                continue;
            var center = _world.Get<Transform2>(obstacle).Position;
            var expanded = radius + _world.Get<Collider>(obstacle).Radius;
            var offset = center - start;
            var projection = Vector2.Dot(offset, direction);
            if (projection <= 0 || projection >= allowed)
                continue;
            var perpendicularSquared = offset.LengthSquared() - projection * projection;
            if (perpendicularSquared >= expanded * expanded)
                continue;
            var entry = projection - MathF.Sqrt(expanded * expanded - perpendicularSquared);
            allowed = MathF.Max(0, entry - 0.01f);
        }
        return start + direction * allowed;
    }

    private void QueueAreaDamage(EntityId source, Vector2 center, float radius, float damage, Faction targetFaction)
    {
        var radiusSquared = radius * radius;
        for (var i = 0; i < _entities.Count; i++)
        {
            var target = _entities[i];
            if (!IsTargetable(target) || !Has<Combatant>(target) ||
                _world.Get<Combatant>(target).Faction != targetFaction)
                continue;
            if (Vector2.DistanceSquared(center, _world.Get<Transform2>(target).Position) <= radiusSquared)
                QueueDamage(target, source, damage, false);
        }
    }

    private void QueueDamage(EntityId target, EntityId source, float amount, bool projectile)
    {
        if (!float.IsFinite(amount) || amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (_damageCount >= _damage.Length)
            throw new InvalidOperationException("Damage work exceeded the deterministic per-tick bound.");
        _damage[_damageCount++] = new DamageRequest(target, source, amount, projectile);
    }

    private void MarkDestroyed(EntityId entity, EntityId source)
    {
        if (!_world.IsAlive(entity) || Has<Destroyed>(entity))
            return;
        _world.Set(entity, new Destroyed(Tick));
        _pendingDestroy.Add(entity);
        AddEvent(CombatEvent.Create(CombatEventKind.EntityDestroyed, Tick, entity, source));
    }

    private EntityId CreateEntity()
    {
        if (_entities.Count >= FlightCombatConstants.MaximumEntities)
            throw new InvalidOperationException("Combat entity capacity reached.");
        var entity = _world.Create();
        var index = _entities.BinarySearch(entity);
        _entities.Insert(index >= 0 ? index : ~index, entity);
        return entity;
    }

    private void AddEvent(CombatEvent value)
    {
        if (_events.Count >= FlightCombatConstants.MaximumEventsPerTick)
            throw new InvalidOperationException("Combat event capacity reached.");
        _events.Add(value);
    }

    private bool Has<T>(EntityId entity) where T : struct =>
        _world.IsAlive(entity) && _world.Store<T>().Has(entity);

    private bool IsTargetable(EntityId entity) =>
        entity != default && _world.IsAlive(entity) && Has<Transform2>(entity) && !Has<Destroyed>(entity);

    private void EnsurePlayer()
    {
        if (_player == default || !_world.IsAlive(_player))
            throw new InvalidOperationException("A live player is required.");
    }

    private float EffectiveEnemyDamage(EntityId entity, float damage) =>
        damage * (Has<Elite>(entity) ? _world.Get<Elite>(entity).DamageMultiplier : 1);

    private int EffectiveEnemyCadence(EntityId entity, int cadence) =>
        Has<Elite>(entity) ? Math.Max(1, (int)MathF.Round(cadence * 0.8f)) : cadence;

    private static TemporaryCombatModifiers DefaultModifiers() =>
        new(1, 1, 1, 1, 0, 0.6f, 0, false);

    private static void ValidateModifiers(TemporaryCombatModifiers value)
    {
        if (!float.IsFinite(value.DamageMultiplier) || value.DamageMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.FireRateMultiplier) || value.FireRateMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.SpeedMultiplier) || value.SpeedMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.MobilityCooldownMultiplier) || value.MobilityCooldownMultiplier is <= 0 or > 10 ||
            value.ExtraProjectiles is < 0 or > 4 || value.PierceCount is < 0 or > 4)
            throw new ArgumentException("Temporary combat modifier values are outside reviewed bounds.");
    }

    private ulong CalculateHash()
    {
        var hash = StableHash.Add(StableHash.Offset, unchecked((ulong)Tick));
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            hash = StableHash.Add(hash, unchecked((ulong)entity.Index));
            hash = StableHash.Add(hash, entity.Generation);
            if (Has<Transform2>(entity))
            {
                var transform = _world.Get<Transform2>(entity);
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Position.X)));
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Position.Y)));
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Rotation)));
            }
            if (Has<Health>(entity))
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(_world.Get<Health>(entity).Current)));
            if (Has<Shield>(entity))
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(_world.Get<Shield>(entity).Current)));
            hash = StableHash.Add(hash, Has<Destroyed>(entity) ? 1UL : 0UL);
        }
        for (var i = 0; i < _events.Count; i++)
        {
            var value = _events[i];
            hash = StableHash.Add(hash, (ulong)value.Kind);
            hash = StableHash.Add(hash, unchecked((ulong)value.Entity.Index));
            hash = StableHash.Add(hash, unchecked((ulong)value.Other.Index));
            hash = AddContentId(hash, value.ContentId);
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(value.Amount)));
        }
        hash = StableHash.Add(hash, (ulong)_pendingCommandCount);
        for (var offset = 0; offset <= FlightCombatConstants.CommandHorizonTicks; offset++)
        {
            var targetTick = Tick + offset;
            var slot = CommandSlot(targetTick);
            if (!_commandOccupied[slot] || _commandSlots[slot].TargetTick != targetTick)
                continue;
            var command = _commandSlots[slot];
            hash = StableHash.Add(hash, unchecked((ulong)command.TargetTick));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.MoveX));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.MoveY));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.AimX));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.AimY));
            hash = StableHash.Add(hash, (ulong)command.Actions);
        }
        return hash;
    }

    private static ulong AddContentId(ulong hash, ContentId id)
    {
        var value = id.Value;
        if (string.IsNullOrEmpty(value))
            return hash;
        Span<byte> bytes = stackalloc byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c > 127)
                throw new InvalidOperationException("Combat content IDs must be ASCII for allocation-free hashing.");
            bytes[i] = (byte)c;
        }
        return StableHash.Add(hash, bytes);
    }

    private int Cell(Vector2 position) =>
        CellCoordinate(position.Y) * GridWidth + CellCoordinate(position.X);

    private static int CellCoordinate(float value) =>
        Math.Clamp((int)MathF.Floor((value - GridOrigin) / GridCellSize), 0, GridWidth - 1);

    private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maximumDelta)
    {
        var delta = target - current;
        var length = delta.Length();
        return length <= maximumDelta || length <= 0.0001f
            ? target
            : current + delta / length * maximumDelta;
    }

    private static Vector2 NormalizeOr(Vector2 value, Vector2 fallback) =>
        value.LengthSquared() > 0.0001f ? Vector2.Normalize(value) : fallback;

    private static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);
    private static Vector2 Rotate(Vector2 value, float angle) =>
        new(value.X * MathF.Cos(angle) - value.Y * MathF.Sin(angle),
            value.X * MathF.Sin(angle) + value.Y * MathF.Cos(angle));

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }

    private readonly record struct DamageRequest(EntityId Target, EntityId Source, float Amount, bool Projectile);
    private readonly record struct CollisionPair(EntityId First, EntityId Second);

    private sealed class DamageRequestComparer : IComparer<DamageRequest>
    {
        public static readonly DamageRequestComparer Instance = new();
        public int Compare(DamageRequest x, DamageRequest y)
        {
            var source = x.Source.CompareTo(y.Source);
            return source != 0 ? source : x.Target.CompareTo(y.Target);
        }
    }

    private sealed class CollisionPairComparer : IComparer<CollisionPair>
    {
        public static readonly CollisionPairComparer Instance = new();
        public int Compare(CollisionPair x, CollisionPair y)
        {
            var first = x.First.CompareTo(y.First);
            return first != 0 ? first : x.Second.CompareTo(y.Second);
        }
    }

    private sealed class DelegateSystem(string name, Action<World, long> update) : ISimulationSystem
    {
        public string Name => name;
        public void Update(World world, long tick) => update(world, tick);
    }
}
