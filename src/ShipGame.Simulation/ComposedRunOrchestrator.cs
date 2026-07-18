using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public enum ComposedRunStatus
{
    Inactive,
    Active,
    Terminal
}

public readonly record struct ComposedPickupView(int X, int Y, ContentId ResourceId, int Quantity);
public readonly record struct ComposedAsteroidView(int CellId, int X, int Y, AsteroidCellKind Kind, bool Broken);

public readonly record struct ComposedRunHud(
    long RunTick,
    RunPhase Phase,
    float Hull,
    float Shield,
    int FerriteHeld,
    int LumenHeld,
    int DataCoresHeld,
    int ObjectiveFerrite,
    int ObjectiveKills,
    int ExtractionProgressTicks,
    int ExtractionHoldTicks,
    int ThreatCap);

/// <summary>
/// P5 composition root: co-steps FlightCombat + WorldRun + mining/loot ECS and produces
/// exactly-once <see cref="RewardProposal"/> via <see cref="RewardHandoff"/>.
/// </summary>
public sealed class ComposedRunOrchestrator
{
    public const int MiningRangeWorldUnits = 70;
    public const int BaseCollectionRadius = 90;
    public const int BasePullSpeed = 8;
    public const int NormalKillFerriteSalvage = 2;

    private readonly World _miningWorld = new();
    private readonly MiningSystem _mining = new();
    private readonly LootGenerationSystem _loot;
    private readonly CollectionSystem _collection = new();
    private readonly Dictionary<int, EntityId> _asteroidEntities = new();
    private readonly List<RunFact> _factBuffer = new(64);
    private readonly List<CombatSnapshot> _snapshotBuffer = new(256);
    private readonly List<CombatRenderItem> _renderBuffer = new(256);
    private EntityId _collector;
    private EntityId _eliteEntity;
    private ulong _nextFactId = 1;
    private long _normalKills;
    private long _eliteKills;
    private long _ferriteCollected;
    private long _cellsBroken;
    private bool _eliteSpawnRequested;
    private bool _rewardMapped;
    private TemporaryModifiers _appliedModifiers = TemporaryModifiers.Identity;
    private int _miningDamagePerTick;
    private string _runId = "";
    private string _environmentId = "";
    private bool _paused;
    private readonly bool _threatEnabled;

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
        _threatEnabled = enableThreatDirector;
        EnvironmentId = environmentId;
        RunSeed = FoundationSimulation.DeriveRunSeed(profileSeed, runIndex);
        _environmentId = environmentId.Value;
        _runId = $"RUN_{runIndex:D6}_{environmentId.Value}";

        var generated = new EncounterGenerator()
            .Generate(GenerationIdentity.Current(environmentId, RunSeed));
        Descriptor = generated.Descriptor;
        Random = new RandomStreams(RunSeed);
        _loot = new(Random);
        WorldRun = new WorldRunSimulation(Descriptor, Random, recoveryProtocols);
        Combat = new FlightCombatSimulation(RunSeed);

        var spawn = CellToWorld(Descriptor.Spawn.Center);
        var weapon = new ContentId(loadout.Effective.Weapon);
        var mobility = statistics.HasBlink ? MobilityBehavior.Blink : MobilityBehavior.Dash;
        Combat.SpawnPlayer(spawn, weapon, mobility);
        foreach (var sector in Descriptor.Sectors)
            Combat.AddSpawnAnchor(CellToWorld(sector.Center), outsideCamera: sector.Kind != SectorKind.Spawn);
        if (enableThreatDirector)
            Combat.ConfigureThreatDirector(90, Math.Clamp(WorldRun.Threat.NormalEnemyCap, 1, 10));

        SeedAsteroids();
        _collector = _miningWorld.Create();
        _miningWorld.Set(_collector, new WorldPosition
        {
            X = (int)MathF.Round(spawn.X),
            Y = (int)MathF.Round(spawn.Y)
        });
        _miningWorld.Set(_collector, new CollectionRadius
        {
            Radius = Math.Max(BaseCollectionRadius, statistics.PickupRadius),
            PullSpeedPerTick = Math.Max(BasePullSpeed, statistics.PullSpeed / 60)
        });

        _miningDamagePerTick = Math.Max(1, (int)Math.Round((double)statistics.MiningDamagePerSecond / WorldRunSimulation.TickRate));
        WorldRun.Upgrades.SeedFromStationPurchases(purchasedUpgradeIds ?? Array.Empty<string>());
        ApplyUpgradeModifiersIfChanged();
        Status = ComposedRunStatus.Active;
        Checkpoints.Add("run_started");
    }

    public ContentId EnvironmentId { get; }
    public ulong RunSeed { get; }
    public FieldDescriptor Descriptor { get; }
    public RandomStreams Random { get; }
    public FlightCombatSimulation Combat { get; }
    public WorldRunSimulation WorldRun { get; }
    public ComposedRunStatus Status { get; private set; }
    public RewardProposal? MappedReward { get; private set; }
    public IReadOnlyList<WorldRunEvent> LastWorldEvents { get; private set; } = Array.Empty<WorldRunEvent>();
    public IReadOnlyList<CombatEvent> LastCombatEvents => Combat.Events;
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
                WorldRunSimulation.ExtractionHoldTicks,
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

        for (var tick = 0; tick < WorldRunSimulation.ExtractionHoldTicks + 2; tick++)
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
        while (WorldRun.RunTick < WorldRunSimulation.DeadlineTick && Status != ComposedRunStatus.Terminal)
            FeedWorldFacts([], paused: false, hullDepleted: false, interact: false, inZone: false);
        if (MappedReward is null)
            throw new InvalidOperationException("Deadline harness did not produce a reward.");
        NoteCheckpoint("deadline_failure");
        return MappedReward;
    }

    private void StepInternal(FlightCommandFrame command)
    {
        _factBuffer.Clear();
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
        }

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
            if (combatEvent.Entity == Combat.Player)
                continue;
            if (combatEvent.Entity == _eliteEntity || Combat.IsElite(combatEvent.Entity))
            {
                _factBuffer.Add(new(_nextFactId++, RunFactKind.EliteDestroyed));
                _eliteKills++;
                NoteCheckpoint("elite_destroyed_combat");
            }
            else
            {
                _factBuffer.Add(new(_nextFactId++, RunFactKind.NormalEnemyDestroyed));
                _factBuffer.Add(new(
                    _nextFactId++,
                    RunFactKind.ResourceCollected,
                    WorldRunIds.Ferrite,
                    NormalKillFerriteSalvage));
                _normalKills++;
                _ferriteCollected += NormalKillFerriteSalvage;
            }
        }
    }

    private void ApplyMining(FlightCommandFrame command)
    {
        if ((command.Actions & FlightAction.Mine) == 0)
            return;
        Combat.TryGetPlayerAim(out var aim);
        var player = SafePlayerSnapshot();
        var origin = player.Position;
        EntityId best = default;
        var bestScore = float.MaxValue;
        foreach (var pair in _asteroidEntities)
        {
            var entity = pair.Value;
            if (!_miningWorld.IsAlive(entity) || !_miningWorld.Store<MineableCell>().Has(entity))
                continue;
            var cell = _miningWorld.Get<MineableCell>(entity);
            if (cell.Broken)
                continue;
            var position = _miningWorld.Get<WorldPosition>(entity);
            var delta = new Vector2(position.X, position.Y) - origin;
            var distance = delta.Length();
            if (distance > MiningRangeWorldUnits)
                continue;
            var direction = distance < 0.001f ? aim : Vector2.Normalize(delta);
            var alignment = 1f - Math.Clamp(Vector2.Dot(aim, direction), -1f, 1f);
            var score = distance + alignment * 40f;
            if (score < bestScore)
            {
                bestScore = score;
                best = entity;
            }
        }

        if (best == default)
            return;

        var damage = Math.Max(
            1,
            (int)Math.Round(_miningDamagePerTick * (_appliedModifiers.MiningDamageBasisPoints / 10_000.0)));
        var broken = _mining.Resolve(_miningWorld, [new MiningContact(Combat.Player, best, damage)]);
        if (broken.Count == 0)
            return;

        foreach (var cell in broken)
        {
            _cellsBroken++;
            var resource = cell.Kind switch
            {
                AsteroidCellKind.Ferrite => WorldRunIds.Ferrite,
                AsteroidCellKind.Lumen => WorldRunIds.Lumen,
                _ => default
            };
            if (resource != default)
                _factBuffer.Add(new(_nextFactId++, RunFactKind.ResourceCellBroken, resource, 1));
        }

        var spawned = _loot.Spawn(_miningWorld, broken, _appliedModifiers.FractureLens);
        _ = spawned;
    }

    private void SyncCollector()
    {
        var player = SafePlayerSnapshot();
        _miningWorld.Set(_collector, new WorldPosition
        {
            X = (int)MathF.Round(player.Position.X),
            Y = (int)MathF.Round(player.Position.Y)
        });
        var radius = BaseCollectionRadius + _appliedModifiers.PickupRadiusFlat;
        var pull = Math.Max(
            1,
            (int)Math.Round(BasePullSpeed * (_appliedModifiers.PullSpeedBasisPoints / 10_000.0)));
        _miningWorld.Set(_collector, new CollectionRadius { Radius = radius, PullSpeedPerTick = pull });
    }

    private void CollectPickups()
    {
        var collected = _collection.Resolve(_miningWorld, _collector);
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
        {
            switch (worldEvent.Kind)
            {
                case WorldRunEventKind.HazardDamageRequested:
                    if (Combat.Player != default)
                        Combat.InflictDamage(Combat.Player, Combat.Player, Math.Max(1, worldEvent.Amount), projectile: false);
                    break;
                case WorldRunEventKind.EliteActivationRequested when !_eliteSpawnRequested:
                    _eliteSpawnRequested = true;
                    _eliteEntity = Combat.SpawnEnemy(
                        new ContentId("ENM_GUNSHIP"),
                        CellToWorld(Descriptor.EliteArena.Center),
                        elite: true);
                    NoteCheckpoint("elite_spawned");
                    break;
                case WorldRunEventKind.DataCoreDropRequested:
                    var player = SafePlayerSnapshot();
                    _loot.SpawnEliteDataCore(
                        _miningWorld,
                        new WorldPosition
                        {
                            X = (int)MathF.Round(player.Position.X),
                            Y = (int)MathF.Round(player.Position.Y)
                        });
                    break;
                case WorldRunEventKind.ObjectiveCompleted:
                    NoteCheckpoint("objective_complete");
                    break;
                case WorldRunEventKind.ExtractionActivated:
                    NoteCheckpoint("extraction_ready");
                    break;
                case WorldRunEventKind.RunSucceeded:
                    NoteCheckpoint("run_succeeded");
                    break;
                case WorldRunEventKind.RunFailed:
                    NoteCheckpoint("run_failed");
                    break;
            }
        }
    }

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
            ResourceCellsBroken: _cellsBroken,
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
            _miningWorld.Set(entity, new MineableCell
            {
                CellId = asteroid.CellId,
                Kind = asteroid.Kind,
                Health = asteroid.Health,
                Broken = false
            });
            _miningWorld.Set(entity, new WorldPosition
            {
                X = (int)MathF.Round(world.X),
                Y = (int)MathF.Round(world.Y)
            });
            _asteroidEntities[asteroid.CellId] = entity;
            // Sparse combat obstacles for cover readability without exhausting entity capacity.
            if (asteroid.ProvidesCompleteCover && asteroid.CellId % 3 == 0)
                Combat.SpawnObstacle(world, 22f);
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
