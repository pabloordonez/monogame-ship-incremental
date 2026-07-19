using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

/// <summary>
/// P5 composition root: co-steps FlightCombat + WorldRun + mining/loot ECS and produces
/// exactly-once <see cref="RewardProposal"/> via <see cref="RewardHandoff"/>.
/// </summary>
public sealed class ComposedRunOrchestrator : IWorldRunEventHost
{
    public const int MiningRangeWorldUnits = 130;
    public const int BaseCollectionRadius = 90;
    public const int BasePullSpeed = 8;
    public const int NormalKillFerriteSalvage = 5;
    /// <summary>Matches drawn medium asteroid sprites (24×24).</summary>
    public const float AsteroidVisualRadius = 12f;
    /// <summary>Aim cone (half-angle) for near-miss mining assist when the ray barely misses.</summary>
    public const float MiningAssistConeDegrees = 18f;

    private readonly World _miningWorld = new();
    private readonly MiningSystem _mining = new();
    private readonly LootGenerationSystem _loot;
    private readonly CollectionSystem _collection = new();
    private readonly WorldRunEventHandlerRegistry _worldEventHandlers = WorldRunEventHandlerRegistry.Create();
    private readonly Dictionary<int, EntityId> _asteroidEntities = new();
    private readonly Dictionary<int, EntityId> _obstacleByCellId = new();
    private readonly List<RunFact> _factBuffer = new(64);
    private readonly List<CombatSnapshot> _snapshotBuffer = new(256);
    private readonly List<CombatRenderItem> _renderBuffer = new(256);
    private readonly List<BrokenAsteroidPresentation> _brokenAsteroidBuffer = new(16);
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<BrokenAsteroidPresentation> _brokenAsteroidView;
    private EntityId _collector;
    private EntityId _eliteEntity;
    private ulong _nextFactId = 1;
    private long _normalKills;
    private long _eliteKills;
    private long _ferriteCollected;
    private bool _eliteSpawnRequested;
    private bool _rewardMapped;
    private TemporaryModifiers _appliedModifiers = TemporaryModifiers.Identity;
    private int _miningDamagePerTick;
    private string _runId = "";
    private string _environmentId = "";
    private bool _paused;
    private readonly bool _threatEnabled;
    private readonly int _basePickupRadius;
    private readonly int _basePullSpeedPerTick;
    private readonly bool _hasScoutDrone;
    private readonly bool _hasTractor;
    private readonly bool _hasSeismicCharge;
    private readonly decimal _ferriteYieldMultiplier;
    private float _droneOrbitAngle;
    private int _droneCooldownTicks;
    private int _seismicCooldownTicks;
    private Vector2 _lastEliteDeathWorldPosition;

    public ComposedRunOrchestrator(
        ContentId environmentId,
        ulong profileSeed,
        long runIndex,
        ResolvedLoadout loadout,
        DerivedShipStatistics statistics,
        bool recoveryProtocols,
        bool enableThreatDirector = true,
        IReadOnlyList<string>? purchasedUpgradeIds = null)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(statistics);
        _brokenAsteroidView = _brokenAsteroidBuffer.AsReadOnly();
        _threatEnabled = enableThreatDirector;
        EnvironmentId = environmentId;
        RunSeed = FoundationSession.DeriveRunSeed(profileSeed, runIndex);
        _environmentId = environmentId.Value;
        _runId = $"RUN_{runIndex:D6}_{environmentId.Value}";

        var generated = new EncounterGenerator()
            .Generate(GenerationIdentity.Current(environmentId, RunSeed));
        Descriptor = generated.Descriptor;
        Random = new RandomStreams(RunSeed);
        _loot = new(Random);
        WorldRun = new WorldRun(Descriptor, Random, recoveryProtocols);
        Combat = new FlightCombatWorld(RunSeed);

        var spawn = CellToWorld(Descriptor.Spawn.Center);
        var weapon = new ContentId(loadout.Effective.Weapon);
        var mobility = statistics.HasBlink ? MobilityBehavior.Blink : MobilityBehavior.Dash;
        var stationUpgrades = RunUpgradeCatalog.Fold(purchasedUpgradeIds ?? Array.Empty<string>());
        var shieldDelaySeconds = (float)statistics.ShieldRechargeDelaySeconds +
                                 stationUpgrades.ShieldDelayTicksFlat / (float)WorldRun.TickRate;
        var spawnStats = new PlayerSpawnStats(
            MaximumHull: statistics.MaximumHull + stationUpgrades.HullFlat,
            MaximumSpeed: statistics.MaximumSpeed,
            ShieldCapacity: statistics.ShieldCapacity + stationUpgrades.ShieldCapacityFlat,
            ShieldRechargePerSecond: (float)statistics.ShieldRechargePerSecond *
                                     (stationUpgrades.ShieldRechargeBasisPoints / 10_000f),
            ShieldDelayTicks: Math.Max(1, (int)MathF.Round(MathF.Max(0.1f, shieldDelaySeconds) * WorldRun.TickRate)),
            ReflectiveFraction: statistics.HasReflectiveShield ? 0.2f : 0f);
        Combat.SpawnPlayer(spawn, weapon, mobility, spawnStats);
        foreach (var sector in Descriptor.Sectors)
            Combat.AddSpawnAnchor(CellToWorld(sector.Center), outsideCamera: sector.Kind != SectorKind.Spawn);
        if (enableThreatDirector)
            Combat.ConfigureThreatDirector(90, Math.Clamp(WorldRun.Threat.NormalEnemyCap, 1, 10));

        SeedAsteroids();
        _basePickupRadius = Math.Max(BaseCollectionRadius, statistics.PickupRadius);
        // Catalog pullSpeed is world-units/sec; /12 keeps tractor snappy toward the ship.
        _basePullSpeedPerTick = statistics.PullSpeed > 0
            ? Math.Max(BasePullSpeed + 4, (int)Math.Round(statistics.PullSpeed / 12.0))
            : BasePullSpeed;
        _hasScoutDrone = statistics.HasScoutDrone;
        _hasTractor = !_hasScoutDrone && statistics.PullSpeed > 0;
        _ferriteYieldMultiplier = statistics.FerriteYieldMultiplier;
        _hasSeismicCharge = loadout.Effective.Mining == ModuleCatalog.MiningCharge;
        var collectorEntity = _miningWorld.Create();
        _ = new Collector(
            _miningWorld,
            collectorEntity,
            new WorldPosition
            {
                X = (int)MathF.Round(spawn.X),
                Y = (int)MathF.Round(spawn.Y)
            },
            _basePickupRadius,
            _basePullSpeedPerTick);
        _collector = collectorEntity;
        ActiveCollectionRadius = _basePickupRadius;
        ActivePullSpeedPerTick = _basePullSpeedPerTick;

        _miningDamagePerTick = Math.Max(1, (int)Math.Round((double)statistics.MiningDamagePerSecond / WorldRun.TickRate));
        WorldRun.Upgrades.SeedFromStationPurchases(purchasedUpgradeIds ?? Array.Empty<string>());
        ApplyUpgradeModifiersIfChanged();
        Status = ComposedRunStatus.Active;
        Checkpoints.Add("run_started");
    }

    public ContentId EnvironmentId { get; }
    public ulong RunSeed { get; }
    public FieldDescriptor Descriptor { get; }
    public RandomStreams Random { get; }
    public FlightCombatWorld Combat { get; }
    public WorldRun WorldRun { get; }
    public ComposedRunStatus Status { get; private set; }
    public RewardProposal? MappedReward { get; private set; }
    public IReadOnlyList<WorldRunEvent> LastWorldEvents { get; private set; } = Array.Empty<WorldRunEvent>();
    public IReadOnlyList<CombatEvent> LastCombatEvents => Combat.Events;
    public MiningPresentationState LastMiningPresentation { get; private set; }
    public ScoutDronePresentation LastScoutDronePresentation { get; private set; }
    public int ActiveCollectionRadius { get; private set; }
    public int ActivePullSpeedPerTick { get; private set; }
    public IReadOnlyList<BrokenAsteroidPresentation> LastBrokenAsteroids => _brokenAsteroidView;
    public List<string> Checkpoints { get; } = [];
    public string RunId => _runId;

    public ComposedRunHud Hud
    {
        get
        {
            var player = Combat.Player != default && Combat.Tick >= 0
                ? SafePlayerSnapshot()
                : default;
            return new(
                WorldRun.RunTick,
                WorldRun.Phase,
                player.Hull,
                player.Shield,
                WorldRun.HeldResources.Ferrite,
                WorldRun.HeldResources.Lumen,
                WorldRun.HeldResources.DataCores,
                WorldRun.Objective.FerriteCollected,
                WorldRun.Objective.NormalEnemiesDestroyed,
                WorldRun.ExtractionProgressTicks,
                WorldRun.ExtractionHoldTicks,
                WorldRun.Threat.NormalEnemyCap);
        }
    }

    public IReadOnlyList<CombatSnapshot> LiveCombatSnapshots
    {
        get
        {
            Combat.CollectSnapshots(_snapshotBuffer);
            return _snapshotBuffer;
        }
    }

    public IReadOnlyList<CombatRenderItem> LiveRenderItems
    {
        get
        {
            Combat.CollectRenderItems(_renderBuffer);
            return _renderBuffer;
        }
    }

    public IEnumerable<ComposedAsteroidView> Asteroids
    {
        get
        {
            foreach (var asteroid in Descriptor.AsteroidCells)
            {
                var broken = _asteroidEntities.TryGetValue(asteroid.CellId, out var entity) &&
                             _miningWorld.IsAlive(entity) &&
                             _miningWorld.Store<MineableCell>().Has(entity) &&
                             _miningWorld.Get<MineableCell>(entity).Broken;
                var world = CellToWorld(asteroid.Position);
                yield return new(asteroid.CellId, (int)world.X, (int)world.Y, asteroid.Kind, broken);
            }
        }
    }

    public bool TryGetEliteWorldPosition(out Vector2 position)
    {
        position = default;
        if (_eliteEntity == default || !Combat.IsElite(_eliteEntity))
            return false;
        try
        {
            var snapshot = Combat.Snapshot(_eliteEntity);
            if (snapshot.Destroyed)
                return false;
            position = snapshot.Position;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryGetNearestHostileWorldPosition(Vector2 from, out Vector2 position)
    {
        position = default;
        var best = float.MaxValue;
        var found = false;
        foreach (var item in LiveRenderItems)
        {
            if (item.Kind != CombatRenderKind.EnemyShip || item.Elite)
                continue;
            var delta = item.Position - from;
            var distanceSquared = delta.LengthSquared();
            if (distanceSquared >= best)
                continue;
            best = distanceSquared;
            position = item.Position;
            found = true;
        }

        return found;
    }

    public IEnumerable<ComposedPickupView> Pickups
    {
        get
        {
            foreach (var pickup in _miningWorld.Query<Collectible, WorldPosition>())
            {
                var item = _miningWorld.Get<Collectible>(pickup);
                if (item.Credited)
                    continue;
                var position = _miningWorld.Get<WorldPosition>(pickup);
                yield return new(position.X, position.Y, item.ResourceId, item.Quantity);
            }
        }
    }

    public bool Queue(FlightCommandFrame command) => Combat.Queue(command);

    public void SetPaused(bool paused) => _paused = paused;

    public void Step(FlightCommandFrame? command = null)
    {
        if (Status is ComposedRunStatus.Inactive or ComposedRunStatus.Terminal)
            return;
        var frame = command ?? FlightCommandFrame.Neutral(Combat.Tick);
        StepInternal(frame);
    }

    /// <summary>
    /// Deterministic harness completion for golden traces / smoke: injects objective facts,
    /// defeats elite, collects data core, and extracts. Uses production WorldRun / RewardHandoff paths only.
    /// </summary>
    public RewardProposal CompleteViaHarness(bool succeed = true)
    {
        if (Status == ComposedRunStatus.Terminal && MappedReward is not null)
            return MappedReward;

        if (!succeed)
        {
            Combat.InflictDamage(Combat.Player, Combat.Player, 10_000f, projectile: false);
            for (var i = 0; i < 8 && Status != ComposedRunStatus.Terminal; i++)
                Step(FlightCommandFrame.Neutral(Combat.Tick));
            if (MappedReward is null)
                throw new InvalidOperationException("Hull-failure harness did not produce a reward.");
            return MappedReward;
        }

        var facts = new List<RunFact>
        {
            new(_nextFactId++, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, 30)
        };
        _ferriteCollected += 30;
        for (var i = 0; i < 8; i++)
        {
            facts.Add(new(_nextFactId++, RunFactKind.NormalEnemyDestroyed));
            facts.Add(new(_nextFactId++, RunFactKind.ResourceCollected, WorldRunIds.Ferrite, NormalKillFerriteSalvage));
            _normalKills++;
            _ferriteCollected += NormalKillFerriteSalvage;
        }

        FeedWorldFacts(facts, paused: false, hullDepleted: false, interact: false, inZone: false);
        NoteCheckpoint("objective_complete");

        FeedWorldFacts(
            [new(_nextFactId++, RunFactKind.EliteDestroyed)],
            paused: false,
            hullDepleted: false,
            interact: false,
            inZone: false);
        _eliteKills++;
        NoteCheckpoint("elite_defeated");

        FeedWorldFacts(
            [new(_nextFactId++, RunFactKind.ResourceCollected, WorldRunIds.DataCore, 1)],
            paused: false,
            hullDepleted: false,
            interact: false,
            inZone: false);
        NoteCheckpoint("extraction_ready");

        for (var tick = 0; tick < WorldRun.ExtractionHoldTicks + 2; tick++)
        {
            FeedWorldFacts([], paused: false, hullDepleted: false, interact: true, inZone: true);
            if (Status == ComposedRunStatus.Terminal)
                break;
        }

        if (MappedReward is null)
            throw new InvalidOperationException("Extraction harness did not produce a reward.");
        NoteCheckpoint("extracted");
        return MappedReward;
    }

    public RewardProposal FailByDeadlineHarness()
    {
        while (WorldRun.RunTick < WorldRun.DeadlineTick && Status != ComposedRunStatus.Terminal)
            FeedWorldFacts([], paused: false, hullDepleted: false, interact: false, inZone: false);
        if (MappedReward is null)
            throw new InvalidOperationException("Deadline harness did not produce a reward.");
        NoteCheckpoint("deadline_failure");
        return MappedReward;
    }

    private void StepInternal(FlightCommandFrame command)
    {
        _factBuffer.Clear();
        _brokenAsteroidBuffer.Clear();
        LastMiningPresentation = default;
        var paused = _paused;

        if (!paused)
        {
            if (_threatEnabled)
                Combat.ConfigureThreatDirector(90, Math.Clamp(WorldRun.Threat.NormalEnemyCap, 1, 10));
            Combat.Queue(command);
            Combat.Step();
            TranslateCombatEvents();
            ApplyMining(command);
            SyncCollector();
            CollectPickups();
            UpdateScoutDrone();
        }
        else
            LastScoutDronePresentation = default;

        var player = SafePlayerSnapshot();
        var playerCell = WorldToCell(player.Position);
        var inZone = DistanceSquared(playerCell, Descriptor.Extraction.Center) <= 2 * 2;
        var behindCover = Descriptor.AsteroidCells.Any(cell =>
            cell.ProvidesCompleteCover &&
            cell.Position.X == playerCell.X &&
            cell.Position.Y == playerCell.Y &&
            !IsAsteroidBroken(cell.CellId));
        var hullDepleted = player.Destroyed || player.Hull <= 0;

        LastWorldEvents = WorldRun.Step(new WorldRunTickInput(
            Paused: paused,
            PlayerHullDepleted: hullDepleted,
            PlayerInExtractionZone: inZone,
            InteractHeld: (command.Actions & FlightAction.Interact) != 0,
            PlayerCell: playerCell,
            BehindLargeAsteroid: behindCover,
            Facts: _factBuffer.Count == 0 ? null : _factBuffer.ToArray()));

        HandleWorldEvents(LastWorldEvents);

        if (WorldRun.IsTerminal)
            FinalizeReward();
    }

    private void FeedWorldFacts(
        IReadOnlyList<RunFact> facts,
        bool paused,
        bool hullDepleted,
        bool interact,
        bool inZone)
    {
        var player = SafePlayerSnapshot();
        LastWorldEvents = WorldRun.Step(new WorldRunTickInput(
            Paused: paused,
            PlayerHullDepleted: hullDepleted,
            PlayerInExtractionZone: inZone,
            InteractHeld: interact,
            PlayerCell: WorldToCell(player.Position),
            BehindLargeAsteroid: false,
            Facts: facts));
        HandleWorldEvents(LastWorldEvents);
        if (WorldRun.IsTerminal)
            FinalizeReward();
    }

    private void TranslateCombatEvents()
    {
        foreach (var combatEvent in Combat.Events)
        {
            if (combatEvent.Kind != CombatEventKind.EntityDestroyed)
                continue;
            if (combatEvent.Entity == Combat.Player || !Combat.IsHostileShip(combatEvent.Entity))
                continue;

            var deathPos = Combat.TryGetPosition(combatEvent.Entity, out var position)
                ? position
                : SafePlayerSnapshot().Position;

            if (combatEvent.Entity == _eliteEntity || Combat.IsElite(combatEvent.Entity))
            {
                _lastEliteDeathWorldPosition = deathPos;
                _factBuffer.Add(new(_nextFactId++, RunFactKind.EliteDestroyed));
                _eliteKills++;
                NoteCheckpoint("elite_destroyed_combat");
            }
            else
            {
                _factBuffer.Add(new(_nextFactId++, RunFactKind.NormalEnemyDestroyed));
                _normalKills++;
                _loot.SpawnSalvageBurst(
                    _miningWorld,
                    new WorldPosition
                    {
                        X = (int)MathF.Round(deathPos.X),
                        Y = (int)MathF.Round(deathPos.Y)
                    },
                    WorldRun.RunTick,
                    NormalKillFerriteSalvage);
            }
        }
    }

    private void ApplyMining(FlightCommandFrame command)
    {
        if (_seismicCooldownTicks > 0)
            _seismicCooldownTicks--;
        if ((command.Actions & FlightAction.Mine) == 0)
            return;
        Combat.TryGetPlayerAim(out var aim);
        var player = SafePlayerSnapshot();
        var origin = player.Position;
        var aimDir = aim.LengthSquared() > 0.01f ? Vector2.Normalize(aim) : Vector2.UnitX;

        if (_hasSeismicCharge)
        {
            ApplySeismicCharge(origin, aimDir);
            return;
        }

        if (!TryPickMiningTarget(origin, aimDir, out var best, out var bestPosition, out var bestKind, out var tipDistance))
        {
            LastMiningPresentation = new MiningPresentationState(
                Active: true,
                Hit: false,
                Origin: origin,
                HitPosition: origin + aimDir * MiningRangeWorldUnits,
                HitDistance: MiningRangeWorldUnits,
                Kind: default);
            return;
        }

        var tipPosition = origin + aimDir * tipDistance;
        LastMiningPresentation = new MiningPresentationState(
            Active: true,
            Hit: true,
            Origin: origin,
            HitPosition: tipPosition,
            HitDistance: tipDistance,
            Kind: bestKind);

        var damage = Math.Max(
            1,
            (int)Math.Round(_miningDamagePerTick * (_appliedModifiers.MiningDamageBasisPoints / 10_000.0)));
        CommitBrokenCells(_mining.Resolve(_miningWorld, [new MiningContact(Combat.Player, best, damage)]));
        _ = bestPosition;
    }

    /// <summary>
    /// Prefer true aim-ray / circle hits; fall back to a narrow cone assist for near misses.
    /// Range is measured to the asteroid surface along the aim ray (or center distance for assist).
    /// </summary>
    private bool TryPickMiningTarget(
        Vector2 origin,
        Vector2 aimDir,
        out EntityId best,
        out Vector2 bestPosition,
        out AsteroidCellKind bestKind,
        out float tipDistance)
    {
        best = default;
        bestPosition = default;
        bestKind = AsteroidCellKind.Ordinary;
        tipDistance = MiningRangeWorldUnits;
        var bestRayDistance = float.MaxValue;
        var bestAssistScore = float.MaxValue;
        EntityId assist = default;
        var assistPosition = Vector2.Zero;
        var assistKind = AsteroidCellKind.Ordinary;
        float assistTip = MiningRangeWorldUnits;
        var minDot = MathF.Cos(MiningAssistConeDegrees * MathF.PI / 180f);

        foreach (var pair in _asteroidEntities)
        {
            var entity = pair.Value;
            if (!_miningWorld.IsAlive(entity) || !_miningWorld.Store<MineableCell>().Has(entity))
                continue;
            var cell = _miningWorld.Get<MineableCell>(entity);
            if (cell.Broken)
                continue;
            var position = _miningWorld.Get<WorldPosition>(entity);
            var world = new Vector2(position.X, position.Y);
            var toCenter = world - origin;
            var centerDistance = toCenter.Length();
            if (centerDistance - AsteroidVisualRadius > MiningRangeWorldUnits)
                continue;

            if (FlightCombatMath.TryRayCircleEntry(origin, aimDir, world, AsteroidVisualRadius, out var entry) &&
                entry <= MiningRangeWorldUnits &&
                entry < bestRayDistance)
            {
                bestRayDistance = entry;
                best = entity;
                bestPosition = world;
                bestKind = cell.Kind;
                tipDistance = entry;
            }

            if (centerDistance > 0.001f)
            {
                var facing = Vector2.Dot(aimDir, toCenter / centerDistance);
                if (facing >= minDot)
                {
                    var score = centerDistance;
                    if (score < bestAssistScore)
                    {
                        bestAssistScore = score;
                        assist = entity;
                        assistPosition = world;
                        assistKind = cell.Kind;
                        assistTip = MathF.Max(0f, centerDistance - AsteroidVisualRadius);
                    }
                }
            }
        }

        if (best != default)
            return true;
        if (assist == default)
            return false;
        best = assist;
        bestPosition = assistPosition;
        bestKind = assistKind;
        tipDistance = MathF.Min(assistTip, MiningRangeWorldUnits);
        return true;
    }

    private void ApplySeismicCharge(Vector2 origin, Vector2 aimDir)
    {
        const float seismicRange = 300f;
        const float seismicRadius = 110f;
        const int seismicMiningDamage = 65;
        const float seismicCombatDamage = 12f;
        var aimPoint = origin + aimDir * Math.Min(seismicRange, 180f);
        LastMiningPresentation = new MiningPresentationState(
            Active: true,
            Hit: _seismicCooldownTicks <= 0,
            Origin: origin,
            HitPosition: aimPoint,
            HitDistance: Vector2.Distance(origin, aimPoint),
            Kind: AsteroidCellKind.Ordinary);

        if (_seismicCooldownTicks > 0)
            return;

        _seismicCooldownTicks = 3 * WorldRun.TickRate;
        var contacts = new List<MiningContact>();
        var miningDamage = Math.Max(
            1,
            (int)Math.Round(seismicMiningDamage * (_appliedModifiers.MiningDamageBasisPoints / 10_000.0)));
        foreach (var pair in _asteroidEntities)
        {
            var entity = pair.Value;
            if (!_miningWorld.IsAlive(entity) || !_miningWorld.Store<MineableCell>().Has(entity))
                continue;
            var cell = _miningWorld.Get<MineableCell>(entity);
            if (cell.Broken)
                continue;
            var position = _miningWorld.Get<WorldPosition>(entity);
            var world = new Vector2(position.X, position.Y);
            if (Vector2.Distance(origin, world) > seismicRange)
                continue;
            if (Vector2.Distance(aimPoint, world) > seismicRadius)
                continue;
            contacts.Add(new MiningContact(Combat.Player, entity, miningDamage));
        }

        CommitBrokenCells(_mining.Resolve(_miningWorld, contacts));
        Combat.CollectSnapshots(_snapshotBuffer);
        var radiusSq = seismicRadius * seismicRadius;
        foreach (var snap in _snapshotBuffer)
        {
            if (snap.Faction != Faction.Hostile || snap.Destroyed || snap.Hull <= 0)
                continue;
            if (Vector2.DistanceSquared(aimPoint, snap.Position) > radiusSq)
                continue;
            Combat.InflictDamage(snap.Entity, Combat.Player, seismicCombatDamage, projectile: false);
        }
    }

    private void CommitBrokenCells(IReadOnlyList<CellBrokenFact> broken)
    {
        if (broken.Count == 0)
            return;

        foreach (var cell in broken)
        {
            var resource = cell.Kind switch
            {
                AsteroidCellKind.Ferrite => WorldRunIds.Ferrite,
                AsteroidCellKind.Lumen => WorldRunIds.Lumen,
                _ => default
            };
            _factBuffer.Add(new(_nextFactId++, RunFactKind.ResourceCellBroken, resource, 1));
            var breakPosition = Vector2.Zero;
            if (_miningWorld.IsAlive(cell.Cell) && _miningWorld.Store<WorldPosition>().Has(cell.Cell))
            {
                var pos = _miningWorld.Get<WorldPosition>(cell.Cell);
                breakPosition = new Vector2(pos.X, pos.Y);
            }

            _brokenAsteroidBuffer.Add(new BrokenAsteroidPresentation(breakPosition, cell.Kind));
            if (_obstacleByCellId.Remove(cell.CellId, out var obstacle))
                Combat.DestroyEntity(obstacle);
        }

        _ = _loot.Spawn(
            _miningWorld,
            broken,
            WorldRun.RunTick,
            _appliedModifiers.FractureLens,
            _ferriteYieldMultiplier);
    }

    private void SyncCollector()
    {
        var player = SafePlayerSnapshot();
        _miningWorld.Set(_collector, new WorldPosition
        {
            X = (int)MathF.Round(player.Position.X),
            Y = (int)MathF.Round(player.Position.Y)
        });
        var radius = _basePickupRadius + _appliedModifiers.PickupRadiusFlat;
        var pull = Math.Max(
            1,
            (int)Math.Round(_basePullSpeedPerTick * (_appliedModifiers.PullSpeedBasisPoints / 10_000.0)));
        ActiveCollectionRadius = radius;
        ActivePullSpeedPerTick = pull;
        _miningWorld.Set(_collector, new CollectionRadius { Radius = radius, PullSpeedPerTick = pull });
    }

    private void UpdateScoutDrone()
    {
        LastScoutDronePresentation = default;
        if (!_hasScoutDrone)
            return;

        var player = SafePlayerSnapshot();
        if (player.Destroyed || player.Hull <= 0)
            return;

        _droneOrbitAngle += 2.4f / WorldRun.TickRate;
        const float orbitRadius = 48f;
        var offset = new Vector2(MathF.Cos(_droneOrbitAngle), MathF.Sin(_droneOrbitAngle)) * orbitRadius;
        var dronePos = player.Position + offset;

        EntityId target = default;
        var targetPos = dronePos;
        var bestDistSq = ScoutDroneRange * ScoutDroneRange;
        Combat.CollectSnapshots(_snapshotBuffer);
        foreach (var snap in _snapshotBuffer)
        {
            if (snap.Faction != Faction.Hostile || snap.Destroyed || snap.Hull <= 0)
                continue;
            var distSq = Vector2.DistanceSquared(dronePos, snap.Position);
            if (distSq >= bestDistSq)
                continue;
            bestDistSq = distSq;
            target = snap.Entity;
            targetPos = snap.Position;
        }

        var rotation = target != default
            ? MathF.Atan2(targetPos.Y - dronePos.Y, targetPos.X - dronePos.X)
            : _droneOrbitAngle + MathF.PI * 0.5f;

        var fired = false;
        if (_droneCooldownTicks > 0)
            _droneCooldownTicks--;
        else if (target != default)
        {
            var aim = targetPos - dronePos;
            Combat.SpawnScoutProjectile(dronePos, aim, ScoutDroneDamage, ScoutDroneProjectileSpeed, ScoutDroneRange);
            _droneCooldownTicks = ScoutDroneCooldownTicks;
            fired = true;
        }

        LastScoutDronePresentation = new ScoutDronePresentation(true, dronePos, rotation, fired);
    }

    private const float ScoutDroneRange = 450f;
    private const float ScoutDroneDamage = 8f;
    private const float ScoutDroneProjectileSpeed = 500f;
    private const int ScoutDroneCooldownTicks = (int)(0.8f * WorldRun.TickRate);

    private void CollectPickups()
    {
        var collected = _collection.Resolve(_miningWorld, _collector, WorldRun.RunTick);
        foreach (var item in collected)
        {
            _factBuffer.Add(new(_nextFactId++, RunFactKind.ResourceCollected, item.ResourceId, item.Quantity));
            if (item.ResourceId == WorldRunIds.Ferrite)
                _ferriteCollected += item.Quantity;
        }
    }

    private void HandleWorldEvents(IReadOnlyList<WorldRunEvent> events)
    {
        foreach (var worldEvent in events)
            _worldEventHandlers.Dispatch(in worldEvent, this);
    }

    EntityId IWorldRunEventHost.PlayerEntity => Combat.Player;

    Vector2 IWorldRunEventHost.EliteArenaWorldCenter => CellToWorld(Descriptor.EliteArena.Center);

    Vector2 IWorldRunEventHost.PlayerWorldPosition => SafePlayerSnapshot().Position;

    Vector2 IWorldRunEventHost.LastEliteDeathWorldPosition => _lastEliteDeathWorldPosition;

    public bool HasTractorUtility => _hasTractor;

    void IWorldRunEventHost.InflictDamage(EntityId target, EntityId source, float amount, bool projectile) =>
        Combat.InflictDamage(target, source, amount, projectile);

    bool IWorldRunEventHost.TrySpawnEliteEnemy(ContentId enemyId, Vector2 worldPosition, out EntityId eliteEntity)
    {
        eliteEntity = default;
        if (_eliteSpawnRequested)
            return false;
        _eliteSpawnRequested = true;
        _eliteEntity = Combat.SpawnEnemy(enemyId, worldPosition, elite: true);
        eliteEntity = _eliteEntity;
        return true;
    }

    void IWorldRunEventHost.SpawnEliteDataCore(WorldPosition position) =>
        _loot.SpawnEliteDataCore(_miningWorld, position, WorldRun.RunTick);

    void IWorldRunEventHost.NoteCheckpoint(string name) => NoteCheckpoint(name);

    private void ApplyUpgradeModifiersIfChanged()
    {
        var modifiers = WorldRun.Upgrades.Modifiers;
        if (Checkpoints.Contains("upgrade_applied") && modifiers.Equals(_appliedModifiers))
            return;
        _appliedModifiers = modifiers;
        Combat.GrantTemporaryModifiers(RunUpgradeCatalog.ToCombatModifiers(modifiers));
        NoteCheckpoint("upgrade_applied");
    }

    private void FinalizeReward()
    {
        if (_rewardMapped || WorldRun.Reward is null)
            return;
        var ion = string.Equals(_environmentId, MetaContentIds.IonVeil, StringComparison.Ordinal) &&
                  WorldRun.Reward.Outcome == RunOutcome.Success
            ? 1
            : 0;
        var counters = new LifetimeCounters(
            Extractions: WorldRun.Reward.Outcome == RunOutcome.Success ? 1 : 0,
            NormalKills: _normalKills,
            EliteKills: _eliteKills,
            FerriteCollected: Math.Max(_ferriteCollected, WorldRun.HeldResources.Ferrite),
            ResourceCellsBroken: WorldRun.ResourceCellsBroken,
            IonVeilExtractions: ion);
        MappedReward = RewardHandoff.ToProfileProposal(
            WorldRun.Reward,
            _runId,
            _environmentId,
            counters);
        _rewardMapped = true;
        Status = ComposedRunStatus.Terminal;
        NoteCheckpoint("reward_mapped");
    }

    private void SeedAsteroids()
    {
        foreach (var asteroid in Descriptor.AsteroidCells)
        {
            var entity = _miningWorld.Create();
            var world = CellToWorld(asteroid.Position);
            _ = new AsteroidCell(
                _miningWorld,
                entity,
                asteroid.CellId,
                asteroid.Kind,
                asteroid.Health,
                new WorldPosition
                {
                    X = (int)MathF.Round(world.X),
                    Y = (int)MathF.Round(world.Y)
                });
            _asteroidEntities[asteroid.CellId] = entity;
            // One combat obstacle per rock so visuals match collision; torn down when mined.
            _obstacleByCellId[asteroid.CellId] = Combat.SpawnObstacle(world, AsteroidVisualRadius);
        }
    }

    private bool IsAsteroidBroken(int cellId) =>
        _asteroidEntities.TryGetValue(cellId, out var entity) &&
        _miningWorld.IsAlive(entity) &&
        _miningWorld.Store<MineableCell>().Has(entity) &&
        _miningWorld.Get<MineableCell>(entity).Broken;

    private CombatSnapshot SafePlayerSnapshot()
    {
        try
        {
            return Combat.Snapshot(Combat.Player);
        }
        catch (InvalidOperationException)
        {
            return new CombatSnapshot(Combat.Tick, Combat.Player, Vector2.Zero, 0, Faction.Player, 0, 0, true);
        }
    }

    private void NoteCheckpoint(string name)
    {
        if (!Checkpoints.Contains(name, StringComparer.Ordinal))
            Checkpoints.Add(name);
    }

    private static Vector2 CellToWorld(GridPoint cell) =>
        new(cell.X * FieldDescriptor.WorldUnitsPerCell, cell.Y * FieldDescriptor.WorldUnitsPerCell);

    private static GridPoint WorldToCell(Vector2 position) =>
        new(
            (int)MathF.Floor(position.X / FieldDescriptor.WorldUnitsPerCell),
            (int)MathF.Floor(position.Y / FieldDescriptor.WorldUnitsPerCell));

    private static int DistanceSquared(GridPoint a, GridPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
