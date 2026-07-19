using System.Collections.ObjectModel;
using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

internal sealed class FlightCombatContext
{
    internal const uint PlayerLayer = 1;
    internal const uint HostileLayer = 2;
    internal const uint ProjectileLayer = 4;
    internal const uint ObstacleLayer = 8;
    internal const uint MineLayer = 16;
    internal const int GridWidth = 128;
    internal const int GridCells = GridWidth * GridWidth;
    internal const float GridCellSize = 64;
    internal const float GridOrigin = -4096;

    internal readonly World World = new();
    internal readonly FlightCombatBehaviorRegistry Registry;
    internal readonly EnemyAiStrategyRegistry EnemyAiStrategies;
    internal readonly WeaponStrategyRegistry WeaponStrategies;
    internal readonly EnemyAiCombatActions EnemyAiCombat;
    internal readonly WeaponFireActions WeaponFireActions;
    internal readonly RandomStreams Random;
    internal readonly FlightCommandFrame[] CommandSlots = new FlightCommandFrame[FlightCombatConstants.CommandSlotCount];
    internal readonly bool[] CommandOccupied = new bool[FlightCombatConstants.CommandSlotCount];
    internal readonly List<EntityId> SortedLive = new(FlightCombatConstants.MaximumEntities);
    internal readonly EntityId[] SpatialEntities = new EntityId[FlightCombatConstants.MaximumEntities];
    internal readonly List<EntityId> PendingDestroy = new(FlightCombatConstants.MaximumEntities);
    internal readonly List<CombatEvent> Events = new(FlightCombatConstants.MaximumEventsPerTick);
    internal readonly ReadOnlyCollection<CombatEvent> EventView;
    internal readonly DamageRequest[] Damage = new DamageRequest[FlightCombatConstants.MaximumDamageRequestsPerTick];
    internal readonly DamageRequest[] ExternalDamage = new DamageRequest[FlightCombatConstants.MaximumDamageRequestsPerTick];
    internal readonly CollisionPair[] Pairs = new CollisionPair[FlightCombatConstants.MaximumDamageRequestsPerTick];
    internal readonly int[] GridHeads = new int[GridCells];
    internal readonly int[] GridNext = new int[FlightCombatConstants.MaximumEntities];
    internal readonly List<SpawnAnchor> Anchors = new(64);
    internal int PendingCommandCount;
    internal int DamageCount;
    internal int ExternalDamageCount;
    internal int PairCount;
    internal int SpatialCount;
    internal bool ThreatEnabled;
    internal int ThreatIntervalTicks;
    internal int ThreatCap;
    internal int EliteSpawnCount;
    internal int MaxEliteSpawns = 1;
    /// <summary>When true, rare Ion threat spawns may receive beam/seeker mounts.</summary>
    internal bool RareAdvancedThreatWeapons;
    internal float EnemyHullMultiplier = 1f;
    internal float EnemyDamageMultiplier = 1f;
    internal EntityId Player;
    internal long Tick;
    internal ulong LastStateHash;

    internal FlightCombatContext(ulong seed, FlightCombatBehaviorRegistry? registry = null)
    {
        Registry = registry ?? FlightCombatBehaviorRegistry.CreateMvp();
        EnemyAiStrategies = EnemyAiStrategyRegistry.CreateMvp();
        WeaponStrategies = WeaponStrategyRegistry.CreateMvp();
        Random = new RandomStreams(seed);
        EventView = Events.AsReadOnly();
        EnemyAiCombat = new EnemyAiCombatActions(SpawnHostileProjectile, SpawnMine);
        WeaponFireActions = new WeaponFireActions(
            FindTargetInCone,
            FindTargetsInConeOrdered,
            QueueDamage,
            AddEvent,
            SpawnPlayerProjectiles);
    }

    internal void ClearTickBuffers()
    {
        Events.Clear();
        DamageCount = 0;
        for (var i = 0; i < ExternalDamageCount; i++)
            Damage[DamageCount++] = ExternalDamage[i];
        ExternalDamageCount = 0;
    }

    internal bool TryTakeCommand(long tick, out FlightCommandFrame command)
    {
        var slot = CommandSlot(tick);
        if (CommandOccupied[slot] && CommandSlots[slot].TargetTick == tick)
        {
            command = CommandSlots[slot];
            CommandOccupied[slot] = false;
            PendingCommandCount--;
            return true;
        }
        command = default;
        return false;
    }

    internal static int CommandSlot(long tick) =>
        (int)(tick % FlightCombatConstants.CommandSlotCount);

    internal EntityId CreateEntity()
    {
        if (World.Store<Transform2>().Count >= FlightCombatConstants.MaximumEntities)
            throw new InvalidOperationException("Combat entity capacity reached.");
        return World.Create();
    }

    internal void MarkDestroyed(EntityId entity, EntityId source)
    {
        if (!World.IsAlive(entity) || Has<Destroyed>(entity))
            return;
        World.Set(entity, new Destroyed(Tick));
        PendingDestroy.Add(entity);
        AddEvent(CombatEvent.Create(CombatEventKind.EntityDestroyed, Tick, entity, source));
    }

    internal void QueueDamage(EntityId target, EntityId source, float amount, bool projectile)
    {
        if (!float.IsFinite(amount) || amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (DamageCount >= Damage.Length)
            throw new InvalidOperationException("Damage work exceeded the deterministic per-tick bound.");
        Damage[DamageCount++] = new DamageRequest(target, source, amount, projectile);
    }

    internal void AddEvent(CombatEvent value)
    {
        if (Events.Count >= FlightCombatConstants.MaximumEventsPerTick)
            throw new InvalidOperationException("Combat event capacity reached.");
        Events.Add(value);
    }

    internal bool Has<T>(EntityId entity) where T : struct =>
        World.IsAlive(entity) && World.Store<T>().Has(entity);

    internal bool IsTargetable(EntityId entity) =>
        entity != default && World.IsAlive(entity) && Has<Transform2>(entity) && !Has<Destroyed>(entity);

    internal void RebuildSortedLive()
    {
        World.Store<Transform2>().CopyEntitiesTo(SortedLive);
        SortedLive.Sort();
    }

    internal float EffectiveEnemyDamage(EntityId entity, float damage) =>
        damage * EnemyDamageMultiplier *
        (Has<Elite>(entity) ? World.Get<Elite>(entity).DamageMultiplier : 1);

    internal int EffectiveEnemyCadence(EntityId entity, int cadence) =>
        Has<Elite>(entity) ? Math.Max(1, (int)MathF.Round(cadence * 0.8f)) : cadence;

    internal EntityId SpawnEnemy(
        ContentId enemyId,
        Vector2 position,
        bool elite = false,
        WeaponBehavior? weaponOverride = null)
    {
        if (elite && EliteSpawnCount >= MaxEliteSpawns)
            throw new InvalidOperationException(
                $"MOD_ELITE_PROTOCOL allows at most {MaxEliteSpawns} elite(s) per run.");
        var definition = Registry.Enemy(enemyId);
        var entity = CreateEntity();
        var weapon = weaponOverride ?? WeaponBehavior.Pulse;
        _ = new Enemy(World, entity, enemyId, definition, position, Player, elite, EnemyHullMultiplier, weapon);
        if (elite)
        {
            EliteSpawnCount++;
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

    internal void SpawnPlayerProjectiles(PlayerProjectileSpawnRequest request)
    {
        var direction = NormalizeOr(request.Aim, Vector2.UnitX);
        var definition = request.Definition;
        var modifiers = request.Modifiers;
        var count = definition.BurstCount + modifiers.ExtraProjectiles;
        var isSeeker = definition.Behavior == WeaponBehavior.Seeker;
        // Catalog: pulse fork 85%, seeker fork 60%. Seeker always uses missile art.
        var forkMultiplier = isSeeker ? 0.6f : 0.85f;
        var pierce = isSeeker ? 0 : modifiers.PierceCount;
        var detonate = isSeeker && modifiers.PierceCount > 0;
        var homeTarget = isSeeker && request.Homing ? request.Target : default;
        for (var index = 0; index < count; index++)
        {
            var angle = count == 1 ? 0 : (index - (count - 1) * 0.5f) * 0.08f;
            var shotDirection = Rotate(direction, angle);
            var multiplier = index < definition.BurstCount ? 1f : forkMultiplier;
            SpawnProjectile(
                request.Source,
                shotDirection,
                definition.Damage * modifiers.DamageMultiplier * multiplier,
                definition.ProjectileSpeed,
                definition.Range,
                Faction.Player,
                pierce,
                missile: isSeeker,
                homeTarget,
                request.TurnDegreesPerSecond,
                detonateOnHit: detonate);
        }
        AddEvent(CombatEvent.Create(
            CombatEventKind.WeaponFired,
            Tick,
            request.Source,
            request.Target,
            definition.Id,
            amount: count));
    }

    internal void SpawnHostileProjectile(EntityId source, Vector2 direction, float damage, float speed)
    {
        var behavior = Has<WeaponMount>(source)
            ? World.Get<WeaponMount>(source).Behavior
            : WeaponBehavior.Pulse;
        if (behavior == WeaponBehavior.Beam)
        {
            if (Player != default && IsTargetable(Player))
            {
                var origin = World.Get<Transform2>(source).Position;
                var toPlayer = World.Get<Transform2>(Player).Position - origin;
                var range = 560f;
                if (toPlayer.LengthSquared() <= range * range)
                {
                    var aim = NormalizeOr(direction, Vector2.UnitX);
                    var toward = NormalizeOr(toPlayer, aim);
                    if (Vector2.Dot(aim, toward) >= 0.85f)
                        QueueDamage(Player, source, damage * 0.55f, projectile: false);
                }
            }

            AddEvent(CombatEvent.Create(CombatEventKind.WeaponFired, Tick, source, Player));
            return;
        }

        if (behavior == WeaponBehavior.Seeker)
        {
            var target = Player != default && IsTargetable(Player) ? Player : default;
            SpawnProjectile(
                source,
                direction,
                damage,
                MathF.Min(speed, 480f),
                700,
                Faction.Hostile,
                0,
                missile: true,
                target,
                turnDegrees: 140);
            return;
        }

        SpawnProjectile(source, direction, damage, speed, 700, Faction.Hostile, 0, false, default, 0);
    }

    internal EntityId SpawnProjectile(
        EntityId source,
        Vector2 direction,
        float damage,
        float speed,
        float range,
        Faction faction,
        int pierces,
        bool missile,
        EntityId target,
        float turnDegrees,
        Vector2? originOverride = null,
        bool detonateOnHit = false)
    {
        var entity = CreateEntity();
        _ = new CombatProjectile(
            World,
            entity,
            source,
            direction,
            damage,
            speed,
            range,
            faction,
            pierces,
            missile,
            target,
            turnDegrees,
            originOverride,
            detonateOnHit);
        return entity;
    }

    internal void SpawnMine(EntityId owner, Vector2 position, float damage)
    {
        var entity = CreateEntity();
        _ = new CombatMine(World, entity, owner, position, damage);
        AddEvent(CombatEvent.Create(
            CombatEventKind.MineTelegraphed,
            Tick,
            entity,
            owner,
            position: position,
            amount: 75));
    }

    internal void ResolveCollision(CollisionPair pair)
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
            QueueDamage(pair.Second, pair.First, World.Get<ContactDamage>(pair.First).Damage, false);
        if (Has<ContactDamage>(pair.Second))
            QueueDamage(pair.First, pair.Second, World.Get<ContactDamage>(pair.Second).Damage, false);
        if (!firstProjectile && !secondProjectile)
            SeparateBlockingPair(pair.First, pair.Second);
    }

    internal void ResolveProjectileContact(EntityId projectileEntity, EntityId target)
    {
        if (!Has<DamageSource>(projectileEntity) || !Has<Projectile>(projectileEntity) ||
            !Has<Combatant>(target))
            return;
        var source = World.Get<DamageSource>(projectileEntity);
        var targetFaction = World.Get<Combatant>(target).Faction;
        if (target == source.Owner || targetFaction == source.Faction || targetFaction == Faction.Neutral)
        {
            if (Has<Collider>(target) && World.Get<Collider>(target).Layer == ObstacleLayer)
            {
                // Player/hostile fire knocks rocks; mining laser uses a separate path.
                if (Has<Velocity2>(target) && Has<Velocity2>(projectileEntity))
                {
                    var impulse = World.Get<Velocity2>(projectileEntity).Value * 0.18f;
                    ref var rockVelocity = ref World.Get<Velocity2>(target);
                    rockVelocity = new Velocity2(rockVelocity.Value + impulse);
                }

                MarkDestroyed(projectileEntity, source.Owner);
            }

            return;
        }
        ref var projectile = ref World.Get<Projectile>(projectileEntity);
        if (projectile.DetonateOnHit)
        {
            var center = Has<Transform2>(projectileEntity)
                ? World.Get<Transform2>(projectileEntity).Position
                : World.Get<Transform2>(target).Position;
            QueueAreaDamage(source.Owner, center, 55f, source.Damage, Faction.Hostile);
            MarkDestroyed(projectileEntity, source.Owner);
            return;
        }

        QueueDamage(target, source.Owner, source.Damage, source.Projectile);
        if (projectile.RemainingPierces > 0)
            projectile = projectile with { RemainingPierces = projectile.RemainingPierces - 1 };
        else
            MarkDestroyed(projectileEntity, source.Owner);
    }

    internal void SeparateBlockingPair(EntityId first, EntityId second)
    {
        var firstCollider = World.Get<Collider>(first);
        var secondCollider = World.Get<Collider>(second);
        if (!firstCollider.BlocksMovement || !secondCollider.BlocksMovement)
            return;
        var firstObstacle = firstCollider.Layer == ObstacleLayer;
        var secondObstacle = secondCollider.Layer == ObstacleLayer;
        // Ships treat rocks as solid (only the ship is pushed). Two rocks may both move/bounce.
        var firstMovable = Has<Velocity2>(first) && (!firstObstacle || secondObstacle);
        var secondMovable = Has<Velocity2>(second) && (!secondObstacle || firstObstacle);
        if (!firstMovable && !secondMovable)
            return;
        ref var firstTransform = ref World.Get<Transform2>(first);
        ref var secondTransform = ref World.Get<Transform2>(second);
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
            // Simple elastic bounce for drifting asteroid pairs.
            if (firstObstacle && secondObstacle)
            {
                ref var firstVelocity = ref World.Get<Velocity2>(first);
                ref var secondVelocity = ref World.Get<Velocity2>(second);
                var relative = secondVelocity.Value - firstVelocity.Value;
                var closing = Vector2.Dot(relative, normal);
                if (closing < 0f)
                {
                    var impulse = normal * closing;
                    firstVelocity = new Velocity2(firstVelocity.Value + impulse);
                    secondVelocity = new Velocity2(secondVelocity.Value - impulse);
                }
            }
        }
        else if (firstMovable)
            firstTransform = firstTransform with { Position = firstTransform.Position - normal * overlap };
        else
            secondTransform = secondTransform with { Position = secondTransform.Position + normal * overlap };
    }

    internal void GuideProjectile(EntityId entity)
    {
        ref var homing = ref World.Get<Homing>(entity);
        if (!IsTargetable(homing.Target))
        {
            homing = homing with { Target = default };
            return;
        }
        ref var velocity = ref World.Get<Velocity2>(entity);
        var position = World.Get<Transform2>(entity).Position;
        var desired = NormalizeOr(World.Get<Transform2>(homing.Target).Position - position, NormalizeOr(velocity.Value, Vector2.UnitX));
        var currentAngle = MathF.Atan2(velocity.Value.Y, velocity.Value.X);
        var desiredAngle = MathF.Atan2(desired.Y, desired.X);
        var difference = WrapAngle(desiredAngle - currentAngle);
        var maxTurn = homing.TurnRadiansPerSecond * FlightCombatConstants.TickSeconds;
        var angle = currentAngle + Math.Clamp(difference, -maxTurn, maxTurn);
        velocity = new Velocity2(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * homing.Speed);
        // Keep render facing on the velocity heading (seekers home by rewriting velocity only).
        ref var transform = ref World.Get<Transform2>(entity);
        transform = transform with { Rotation = angle };
    }

    internal EntityId FindTargetInCone(EntityId source, Vector2 aim, float range, float halfConeDegrees)
    {
        var ordered = FindTargetsInConeOrdered(source, aim, range, halfConeDegrees, 1);
        return ordered.Count > 0 ? ordered[0] : default;
    }

    internal IReadOnlyList<EntityId> FindTargetsInConeOrdered(
        EntityId source,
        Vector2 aim,
        float range,
        float halfConeDegrees,
        int maxTargets)
    {
        var origin = World.Get<Transform2>(source).Position;
        var direction = NormalizeOr(aim, Vector2.UnitX);
        var minimumDot = halfConeDegrees <= 0 ? 0.999f : MathF.Cos(halfConeDegrees * MathF.PI / 180f);
        var rangeSquared = range * range;
        var scored = new List<(EntityId Id, float DistSq)>(8);
        RebuildSortedLive();
        for (var i = 0; i < SortedLive.Count; i++)
        {
            var candidate = SortedLive[i];
            if (candidate == source || !IsTargetable(candidate) || !Has<Combatant>(candidate))
                continue;
            if (World.Get<Combatant>(candidate).Faction != Faction.Hostile)
                continue;
            var delta = World.Get<Transform2>(candidate).Position - origin;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared > rangeSquared || distanceSquared <= 0.0001f)
                continue;
            var along = Vector2.Dot(direction, delta);
            if (along <= 0f || along > range)
                continue;
            var closestDistSq = distanceSquared - along * along;
            if (closestDistSq < 0f)
                closestDistSq = 0f;
            var radius = Has<Collider>(candidate) ? World.Get<Collider>(candidate).Radius : 0f;
            var rayHits = closestDistSq <= radius * radius;
            var inCone = Vector2.Dot(direction, Vector2.Normalize(delta)) >= minimumDot;
            if (!rayHits && !inCone)
                continue;
            scored.Add((candidate, distanceSquared));
        }

        scored.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));
        var limit = Math.Clamp(maxTargets, 0, scored.Count);
        var result = new EntityId[limit];
        for (var i = 0; i < limit; i++)
            result[i] = scored[i].Id;
        return result;
    }

    internal Vector2 ShortenAgainstObstacles(EntityId mover, Vector2 start, Vector2 requested)
    {
        var direction = requested - start;
        var distance = direction.Length();
        if (distance <= 0.0001f)
            return start;
        direction /= distance;
        var radius = World.Get<Collider>(mover).Radius;
        var allowed = distance;
        RebuildSortedLive();
        for (var i = 0; i < SortedLive.Count; i++)
        {
            var obstacle = SortedLive[i];
            if (!Has<Collider>(obstacle) || !Has<Transform2>(obstacle) ||
                World.Get<Collider>(obstacle).Layer != ObstacleLayer)
                continue;
            var center = World.Get<Transform2>(obstacle).Position;
            var expanded = radius + World.Get<Collider>(obstacle).Radius;
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

    internal void QueueAreaDamage(EntityId source, Vector2 center, float radius, float damage, Faction targetFaction)
    {
        var radiusSquared = radius * radius;
        RebuildSortedLive();
        for (var i = 0; i < SortedLive.Count; i++)
        {
            var target = SortedLive[i];
            if (!IsTargetable(target) || !Has<Combatant>(target) ||
                World.Get<Combatant>(target).Faction != targetFaction)
                continue;
            if (Vector2.DistanceSquared(center, World.Get<Transform2>(target).Position) <= radiusSquared)
                QueueDamage(target, source, damage, false);
        }
    }

    internal int Cell(Vector2 position) =>
        CellCoordinate(position.Y) * GridWidth + CellCoordinate(position.X);

    internal static int CellCoordinate(float value) =>
        Math.Clamp((int)MathF.Floor((value - GridOrigin) / GridCellSize), 0, GridWidth - 1);

    internal ulong CalculateHash()
    {
        RebuildSortedLive();
        var hash = StableHash.Add(StableHash.Offset, unchecked((ulong)Tick));
        for (var i = 0; i < SortedLive.Count; i++)
        {
            var entity = SortedLive[i];
            hash = StableHash.Add(hash, unchecked((ulong)entity.Index));
            hash = StableHash.Add(hash, entity.Generation);
            if (Has<Transform2>(entity))
            {
                var transform = World.Get<Transform2>(entity);
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Position.X)));
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Position.Y)));
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(transform.Rotation)));
            }
            if (Has<Health>(entity))
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(World.Get<Health>(entity).Current)));
            if (Has<Shield>(entity))
                hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(World.Get<Shield>(entity).Current)));
            hash = StableHash.Add(hash, Has<Destroyed>(entity) ? 1UL : 0UL);
        }
        for (var i = 0; i < Events.Count; i++)
        {
            var value = Events[i];
            hash = StableHash.Add(hash, (ulong)value.Kind);
            hash = StableHash.Add(hash, unchecked((ulong)value.Entity.Index));
            hash = StableHash.Add(hash, unchecked((ulong)value.Other.Index));
            hash = AddContentId(hash, value.ContentId);
            hash = StableHash.Add(hash, unchecked((ulong)BitConverter.SingleToInt32Bits(value.Amount)));
        }
        hash = StableHash.Add(hash, (ulong)PendingCommandCount);
        for (var offset = 0; offset <= FlightCombatConstants.CommandHorizonTicks; offset++)
        {
            var targetTick = Tick + offset;
            var slot = CommandSlot(targetTick);
            if (!CommandOccupied[slot] || CommandSlots[slot].TargetTick != targetTick)
                continue;
            var command = CommandSlots[slot];
            hash = StableHash.Add(hash, unchecked((ulong)command.TargetTick));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.MoveX));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.MoveY));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.AimX));
            hash = StableHash.Add(hash, unchecked((ulong)(ushort)command.AimY));
            hash = StableHash.Add(hash, (ulong)command.Actions);
        }
        return hash;
    }

    internal static TemporaryCombatModifiers DefaultModifiers() =>
        new(1, 1, 1, 1, 0, 0.6f, 0, false);

    internal static void ValidateModifiers(TemporaryCombatModifiers value)
    {
        if (!float.IsFinite(value.DamageMultiplier) || value.DamageMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.FireRateMultiplier) || value.FireRateMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.SpeedMultiplier) || value.SpeedMultiplier is <= 0 or > 10 ||
            !float.IsFinite(value.MobilityCooldownMultiplier) || value.MobilityCooldownMultiplier is <= 0 or > 10 ||
            value.ExtraProjectiles is < 0 or > 4 || value.PierceCount is < 0 or > 4)
            throw new ArgumentException("Temporary combat modifier values are outside reviewed bounds.");
    }

    internal static Vector2 MoveTowards(Vector2 current, Vector2 target, float maximumDelta)
    {
        var delta = target - current;
        var length = delta.Length();
        return length <= maximumDelta || length <= 0.0001f
            ? target
            : current + delta / length * maximumDelta;
    }

    internal static Vector2 NormalizeOr(Vector2 value, Vector2 fallback) =>
        FlightCombatMath.NormalizeOr(value, fallback);

    internal static Vector2 Rotate(Vector2 value, float angle) =>
        new(value.X * MathF.Cos(angle) - value.Y * MathF.Sin(angle),
            value.X * MathF.Sin(angle) + value.Y * MathF.Cos(angle));

    internal static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
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

    internal readonly record struct DamageRequest(EntityId Target, EntityId Source, float Amount, bool Projectile);
    internal readonly record struct CollisionPair(EntityId First, EntityId Second);

    internal sealed class DamageRequestComparer : IComparer<DamageRequest>
    {
        public static readonly DamageRequestComparer Instance = new();
        public int Compare(DamageRequest x, DamageRequest y)
        {
            var source = x.Source.CompareTo(y.Source);
            return source != 0 ? source : x.Target.CompareTo(y.Target);
        }
    }

    internal sealed class CollisionPairComparer : IComparer<CollisionPair>
    {
        public static readonly CollisionPairComparer Instance = new();
        public int Compare(CollisionPair x, CollisionPair y)
        {
            var first = x.First.CompareTo(y.First);
            return first != 0 ? first : x.Second.CompareTo(y.Second);
        }
    }
}
