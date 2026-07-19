using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed class ProfileAggregate
{
    public const int MaxSavedIds = 4096;
    private MetaProfileSnapshot _snapshot;

    public ProfileAggregate(MetaProfileSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        _snapshot = Clone(snapshot);
    }

    public MetaProfileSnapshot Snapshot => Clone(_snapshot);

    /// <summary>Restores durable in-memory state after a failed persist (same aggregate instance).</summary>
    public void Restore(MetaProfileSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        _snapshot = Clone(snapshot);
    }

    public static ProfileAggregate CreateNew(ulong profileSeed) =>
        new(new(
            profileSeed,
            0,
            ResourceAmounts.Zero,
            LifetimeCounters.Zero,
            [],
            [],
            [MetaContentIds.CinderBelt],
            ModuleCatalog.Defaults,
            [],
            GameSettings.Default,
            null));

    /// <summary>
    /// Locks the next run index before field entry (P5 host composition).
    /// Idempotent for a given transaction ID.
    /// </summary>
    public ProfileMutationResult BeginRun(string transactionId)
    {
        if (!TryValidateIdentity(transactionId, out var identityError))
            return Rejected("run.invalid-transaction", identityError!);
        var fingerprint = Fingerprint("begin_run", _snapshot.RunIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!TryBegin(transactionId, "begin_run", fingerprint, out var existing))
            return existing!;
        if (_snapshot.RunIndex == long.MaxValue)
            return Rejected("run.index-overflow", "Run index would overflow.");
        var receipts = _snapshot.Transactions
            .Append(new(transactionId, "begin_run", fingerprint))
            .ToArray();
        _snapshot = _snapshot with
        {
            RunIndex = _snapshot.RunIndex + 1,
            Transactions = receipts
        };
        return Applied("run.begun", $"Locked run index {_snapshot.RunIndex}.");
    }

    public ProfileMutationResult CommitAcceptedReward(RewardProposal proposal)
    {
        if (!TryValidateIdentity(proposal.TransactionId, out var identityError))
            return Rejected("reward.invalid-identity", identityError!);
        if (!TryValidateIdentity(proposal.RunId, out var runError))
            return Rejected("reward.invalid-identity", runError!);
        if (string.Equals(proposal.TransactionId, proposal.RunId, StringComparison.Ordinal))
            return Rejected("reward.invalid-identity", "Transaction ID and run ID must differ.");
        if (!MetaContentIds.IsCanonical(proposal.EnvironmentId))
            return Rejected("reward.invalid-identity", "Invalid environment ID.");
        if (!TryBegin(proposal.TransactionId, "reward", Fingerprint(proposal), out var existing))
            return existing!;
        if (!proposal.Earned.IsValid || !proposal.Banked.IsValid ||
            !proposal.Retained.IsValid || !proposal.Lost.IsValid ||
            !proposal.CounterDelta.IsValid)
            return Rejected("reward.invalid-values", "Reward resources and counters must be nonnegative.");
        if (!TrySum(proposal.Banked, proposal.Retained, proposal.Lost, out var accounted) ||
            accounted != proposal.Earned)
            return Rejected("reward.unbalanced", "Banked, retained, and lost resources must exactly account for earned resources.");
        if (proposal.Succeeded && (proposal.CounterDelta.Extractions != 1 || proposal.Retained != ResourceAmounts.Zero) ||
            !proposal.Succeeded && proposal.CounterDelta.Extractions != 0)
            return Rejected("reward.result-mismatch", "Reward counters do not match the terminal result.");
        if (!proposal.Succeeded && proposal.Banked != ResourceAmounts.Zero)
            return Rejected("reward.result-mismatch", "Failed runs cannot bank resources.");
        if (proposal.CounterDelta.IonVeilExtractions > proposal.CounterDelta.Extractions ||
            (proposal.CounterDelta.IonVeilExtractions > 0 &&
             !string.Equals(proposal.EnvironmentId, MetaContentIds.IonVeil, StringComparison.Ordinal)))
            return Rejected("reward.environment-mismatch", "Ion Veil extraction counters require an Ion Veil success.");
        if (_snapshot.Transactions.Any(receipt =>
                receipt.Operation == "reward_run" &&
                string.Equals(receipt.TransactionId, proposal.RunId, StringComparison.Ordinal)))
            return Rejected("reward.run-already-committed", "This run already has a committed reward.");
        if (!ResourceAmounts.TryAdd(proposal.Banked, proposal.Retained, out var credited) ||
            !ResourceAmounts.TryAdd(_snapshot.Balances, credited, out var balances) ||
            !LifetimeCounters.TryAdd(_snapshot.Counters, proposal.CounterDelta, out var counters))
            return Rejected("reward.overflow", "Reward would overflow profile values.");

        var receipts = _snapshot.Transactions
            .Append(new(proposal.TransactionId, "reward", Fingerprint(proposal)))
            .Append(new(proposal.RunId, "reward_run", Fingerprint(proposal)))
            .ToArray();
        _snapshot = _snapshot with
        {
            Balances = balances,
            Counters = counters,
            Transactions = receipts,
            PreviousRun = new(
                proposal.RunId,
                proposal.EnvironmentId,
                proposal.Succeeded,
                proposal.Earned,
                proposal.Banked,
                proposal.Retained,
                proposal.Lost)
        };
        return Applied("reward.committed", "Accepted run reward committed exactly once.");
    }

    public ProfileMutationResult PurchaseResearch(string transactionId, string researchId)
    {
        if (!TryValidateIdentity(transactionId, out var identityError))
            return Rejected("research.invalid-transaction", identityError!);
        var fingerprint = Fingerprint("research", researchId);
        if (!TryBegin(transactionId, "research", fingerprint, out var existing))
            return existing!;
        if (!ResearchCatalog.TryGet(researchId, out var definition))
            return Rejected("research.unknown", $"Unknown research ID '{Safe(researchId)}'.");
        var purchased = _snapshot.PurchasedResearchIds.ToHashSet(StringComparer.Ordinal);
        if (purchased.Contains(researchId))
            return Rejected("research.already-purchased", "Research is already purchased.");
        var missing = definition.Dependencies.Where(dependency => !purchased.Contains(dependency)).ToArray();
        if (missing.Length > 0)
            return Rejected("research.prerequisite", $"Missing prerequisite: {string.Join(", ", missing)}.");
        if (!definition.Gate(_snapshot.Counters))
            return Rejected("research.gate", $"Gate not met: {definition.GateDescription}.");
        if (!ResourceAmounts.TrySubtract(_snapshot.Balances, definition.Cost, out var remaining))
            return Rejected("research.cost", "Insufficient resources.");

        var unlocked = _snapshot.UnlockedEnvironmentIds.ToList();
        if (string.Equals(definition.Grant, MetaContentIds.TravelIonVeil, StringComparison.Ordinal) &&
            !unlocked.Contains(MetaContentIds.IonVeil, StringComparer.Ordinal))
            unlocked.Add(MetaContentIds.IonVeil);

        _snapshot = _snapshot with
        {
            Balances = remaining,
            PurchasedResearchIds = _snapshot.PurchasedResearchIds.Append(researchId).ToArray(),
            UnlockedEnvironmentIds = unlocked,
            Transactions = _snapshot.Transactions.Append(new(transactionId, "research", fingerprint)).ToArray()
        };
        return Applied("research.purchased", $"Purchased {definition.Name}.");
    }

    public ProfileMutationResult PurchaseUpgrade(string transactionId, string upgradeId)
    {
        if (!TryValidateIdentity(transactionId, out var identityError))
            return Rejected("upgrade.invalid-transaction", identityError!);
        var fingerprint = Fingerprint("upgrade", upgradeId);
        if (!TryBegin(transactionId, "upgrade", fingerprint, out var existing))
            return existing!;
        if (!RunUpgradeCatalog.TryGet(upgradeId, out var definition))
            return Rejected("upgrade.unknown", $"Unknown upgrade ID '{Safe(upgradeId)}'.");
        if (_snapshot.PurchasedUpgradeIds.Contains(upgradeId, StringComparer.Ordinal))
            return Rejected("upgrade.already-purchased", "Upgrade is already purchased.");
        if (!ResourceAmounts.TrySubtract(_snapshot.Balances, definition.Cost, out var remaining))
            return Rejected("upgrade.cost", "Insufficient resources.");

        _snapshot = _snapshot with
        {
            Balances = remaining,
            PurchasedUpgradeIds = _snapshot.PurchasedUpgradeIds.Append(upgradeId).ToArray(),
            Transactions = _snapshot.Transactions.Append(new(transactionId, "upgrade", fingerprint)).ToArray()
        };
        return Applied("upgrade.purchased", $"Purchased {upgradeId}.");
    }

    public UpgradePreview InspectUpgrade(string upgradeId)
    {
        if (!RunUpgradeCatalog.TryGet(upgradeId, out var definition))
            throw new KeyNotFoundException($"Unknown upgrade ID '{Safe(upgradeId)}'.");
        var purchased = _snapshot.PurchasedUpgradeIds.Contains(upgradeId, StringComparer.Ordinal);
        var affordable = ResourceAmounts.TrySubtract(_snapshot.Balances, definition.Cost, out _);
        var explanation = purchased
            ? "Purchased."
            : !affordable
                ? "Insufficient resources."
                : "Available for purchase.";
        return new(definition, purchased, affordable, explanation);
    }

    public ResearchPreview InspectResearch(string researchId)
    {
        if (!ResearchCatalog.TryGet(researchId, out var definition))
            throw new KeyNotFoundException($"Unknown research ID '{Safe(researchId)}'.");
        var purchased = _snapshot.PurchasedResearchIds.Contains(researchId, StringComparer.Ordinal);
        var prerequisites = definition.Dependencies.All(dependency =>
            _snapshot.PurchasedResearchIds.Contains(dependency, StringComparer.Ordinal));
        var gate = definition.Gate(_snapshot.Counters);
        var affordable = ResourceAmounts.TrySubtract(_snapshot.Balances, definition.Cost, out _);
        var explanation = purchased
            ? "Purchased."
            : !prerequisites
                ? "Prerequisites are not complete."
                : !gate
                    ? $"Gate not met: {definition.GateDescription}."
                    : !affordable
                        ? "Insufficient resources."
                        : "Available for purchase.";
        return new(definition, purchased, prerequisites, gate, affordable, explanation);
    }

    public ProfileMutationResult EquipModule(string transactionId, ModuleSlot slot, string moduleId)
    {
        if (!TryValidateIdentity(transactionId, out var identityError))
            return Rejected("loadout.invalid-transaction", identityError!);
        var fingerprint = Fingerprint("equip", $"{slot}:{moduleId}");
        if (!TryBegin(transactionId, "equip", fingerprint, out var existing))
            return existing!;
        if (!ModuleCatalog.TryGet(moduleId, out var module))
            return Rejected("loadout.unknown", $"Unknown module ID '{Safe(moduleId)}'.");
        if (module.Slot != slot)
            return Rejected("loadout.incompatible", $"{moduleId} is not compatible with {slot}.");
        if (module.RequiredResearchId is not null &&
            !_snapshot.PurchasedResearchIds.Contains(module.RequiredResearchId, StringComparer.Ordinal))
            return Rejected("loadout.locked", $"Requires {module.RequiredResearchId}.");

        _snapshot = _snapshot with
        {
            RequestedLoadout = _snapshot.RequestedLoadout.With(slot, moduleId),
            Transactions = _snapshot.Transactions.Append(new(transactionId, "equip", fingerprint)).ToArray()
        };
        return Applied("loadout.changed", $"Equipped {moduleId}.");
    }

    public LoadoutPreview InspectModule(ModuleSlot slot, string moduleId)
    {
        var current = DeriveStatistics();
        if (!ModuleCatalog.TryGet(moduleId, out var module))
            return new(slot, moduleId, false, false, false, current, null, "Unknown module.");
        if (module.Slot != slot)
            return new(slot, moduleId, true, false, false, current, null, $"Module belongs in {module.Slot}.");
        var unlocked = module.RequiredResearchId is null ||
                       _snapshot.PurchasedResearchIds.Contains(module.RequiredResearchId, StringComparer.Ordinal);
        if (!unlocked)
            return new(slot, moduleId, true, true, false, current, null, $"Requires {module.RequiredResearchId}.");
        var previewSnapshot = _snapshot with { RequestedLoadout = _snapshot.RequestedLoadout.With(slot, moduleId) };
        var proposed = new ProfileAggregate(previewSnapshot).DeriveStatistics();
        return new(slot, moduleId, true, true, true, current, proposed, "Available to equip.");
    }

    public ProfileMutationResult UpdateSettings(string transactionId, GameSettings settings)
    {
        if (!TryValidateIdentity(transactionId, out var identityError))
            return Rejected("settings.invalid-transaction", identityError!);
        var fingerprint = Fingerprint("settings", settings.ToString());
        if (!TryBegin(transactionId, "settings", fingerprint, out var existing))
            return existing!;
        if (!settings.IsValid)
            return Rejected("settings.invalid", "Volumes must be in the range 0 through 100.");
        _snapshot = _snapshot with
        {
            Settings = settings,
            Transactions = _snapshot.Transactions.Append(new(transactionId, "settings", fingerprint)).ToArray()
        };
        return Applied("settings.changed", "Settings updated.");
    }

    public bool HasCapability(string capabilityId)
    {
        if (!MetaContentIds.IsCanonical(capabilityId))
            return false;
        foreach (var researchId in _snapshot.PurchasedResearchIds)
        {
            if (!ResearchCatalog.TryGet(researchId, out var definition))
                continue;
            if (string.Equals(definition.Grant, capabilityId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public ProfileMutationResult ValidateDestination(string environmentId) =>
        environmentId switch
        {
            MetaContentIds.CinderBelt => Applied("travel.allowed", "Cinder Belt is unlocked."),
            MetaContentIds.IonVeil when HasCapability(MetaContentIds.TravelIonVeil) =>
                Applied("travel.allowed", "Ion Veil capability is present."),
            MetaContentIds.IonVeil =>
                Rejected("travel.capability-required", $"Requires capability {MetaContentIds.TravelIonVeil}."),
            _ => Rejected("travel.unknown", $"Unknown environment ID '{Safe(environmentId)}'.")
        };

    public ResolvedLoadout ResolveLoadout()
    {
        var effective = ModuleCatalog.Defaults;
        var diagnostics = new List<LoadoutDiagnostic>();
        foreach (var slot in Enum.GetValues<ModuleSlot>())
        {
            var requested = _snapshot.RequestedLoadout.For(slot);
            var fallback = ModuleCatalog.DefaultFor(slot);
            string? code = null;
            string? message = null;
            if (!ModuleCatalog.TryGet(requested, out var module))
            {
                code = "loadout.unknown-fallback";
                message = $"Saved module '{Safe(requested)}' is unknown; using {fallback}.";
            }
            else if (module.Slot != slot)
            {
                code = "loadout.incompatible-fallback";
                message = $"Saved module '{requested}' is incompatible with {slot}; using {fallback}.";
            }
            else if (module.RequiredResearchId is not null &&
                     !_snapshot.PurchasedResearchIds.Contains(module.RequiredResearchId, StringComparer.Ordinal))
            {
                code = "loadout.locked-fallback";
                message = $"Saved module '{requested}' is locked; using {fallback}.";
            }
            else
            {
                fallback = requested;
            }
            effective = effective.With(slot, fallback);
            if (code is not null)
                diagnostics.Add(new(slot, requested, fallback, code, message!));
        }
        return new(effective, diagnostics);
    }

    public DerivedShipStatistics DeriveStatistics()
    {
        var purchased = _snapshot.PurchasedResearchIds.ToHashSet(StringComparer.Ordinal);
        var loadout = ResolveLoadout().Effective;
        var baseSpeed = loadout.Engine == ModuleCatalog.EngineBlink ? 200 : 220;
        var shieldReflective = loadout.Shield == ModuleCatalog.ShieldReflective;
        return new(
            100 + (purchased.Contains(ResearchCatalog.HullReinforcement) ? 15 : 0),
            70 + (loadout.Utility == ModuleCatalog.UtilityTractor ? 140 : 0) +
            (purchased.Contains(ResearchCatalog.TractorCalibration) ? 35 : 0),
            loadout.Utility == ModuleCatalog.UtilityTractor ? 260 : 0,
            purchased.Contains(ResearchCatalog.EngineTuning)
                ? decimal.ToInt32(decimal.Round(baseSpeed * 1.08m, 0, MidpointRounding.AwayFromZero))
                : baseSpeed,
            shieldReflective ? 45 : 60,
            shieldReflective ? 10 : 12,
            shieldReflective ? 2.5m : 3m,
            loadout.Weapon switch
            {
                ModuleCatalog.WeaponBeam => 30,
                ModuleCatalog.WeaponSeeker => 32,
                _ => 10
            },
            loadout.Mining == ModuleCatalog.MiningCharge ? 65m / 3m : 25m,
            loadout.Engine == ModuleCatalog.EngineBlink,
            shieldReflective,
            loadout.Utility == ModuleCatalog.UtilityDrone,
            purchased.Contains(ResearchCatalog.MiningAssay) ? 1.15m : 1m,
            purchased.Contains(ResearchCatalog.RecoveryProtocols) ? 50 : 25);
    }

    private bool TryBegin(
        string transactionId,
        string operation,
        ulong fingerprint,
        out ProfileMutationResult? result)
    {
        var receipt = _snapshot.Transactions.FirstOrDefault(item =>
            string.Equals(item.TransactionId, transactionId, StringComparison.Ordinal));
        if (receipt is null)
        {
            result = null;
            return true;
        }
        result = receipt.Operation == operation && receipt.Fingerprint == fingerprint
            ? new(ProfileMutationStatus.Duplicate, "transaction.duplicate", "Transaction was already applied.")
            : Rejected("transaction.conflict", "Transaction ID was reused with different content.");
        return false;
    }

    private static void ValidateSnapshot(MetaProfileSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.RunIndex < 0 || !snapshot.Balances.IsValid || !snapshot.Counters.IsValid)
            throw new ArgumentException("Profile numbers must be nonnegative.", nameof(snapshot));
        if (snapshot.Settings is null || !snapshot.Settings.IsValid || snapshot.RequestedLoadout is null)
            throw new ArgumentException("Profile settings and loadout are required.", nameof(snapshot));
        ValidateBoundedStrings(snapshot.PurchasedResearchIds, "research");
        ValidateBoundedStrings(snapshot.PurchasedUpgradeIds, "upgrades");
        ValidateBoundedStrings(snapshot.UnlockedEnvironmentIds, "environments");
        if (snapshot.Transactions is null || snapshot.Transactions.Count > MaxSavedIds)
            throw new ArgumentException("Profile transaction history is invalid.", nameof(snapshot));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var receipt in snapshot.Transactions)
        {
            if (receipt is null ||
                !TryValidateIdentity(receipt.TransactionId, out _) ||
                !MetaContentIds.IsCanonical(receipt.Operation) ||
                !seen.Add(receipt.TransactionId))
                throw new ArgumentException("Profile transaction receipt is invalid.", nameof(snapshot));
        }
        foreach (var slot in Enum.GetValues<ModuleSlot>())
        {
            var id = snapshot.RequestedLoadout.For(slot);
            if (id is null || id.Length > 128)
                throw new ArgumentException("Saved module IDs are bounded to 128 characters.", nameof(snapshot));
        }
    }

    private static void ValidateBoundedStrings(IReadOnlyList<string> values, string member)
    {
        if (values is null || values.Count > MaxSavedIds ||
            values.Any(value => value is null || value.Length > 128) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new ArgumentException($"Profile {member} IDs are invalid.");
    }

    private static MetaProfileSnapshot Clone(MetaProfileSnapshot snapshot) =>
        snapshot with
        {
            PurchasedResearchIds = snapshot.PurchasedResearchIds.ToArray(),
            PurchasedUpgradeIds = snapshot.PurchasedUpgradeIds.ToArray(),
            UnlockedEnvironmentIds = snapshot.UnlockedEnvironmentIds.ToArray(),
            Transactions = snapshot.Transactions.ToArray()
        };

    private static bool TryValidateIdentity(string value, out string? error)
    {
        if (!MetaContentIds.IsCanonical(value))
        {
            error = "Transaction and run IDs must be canonical and at most 128 characters.";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TrySum(
        ResourceAmounts first,
        ResourceAmounts second,
        ResourceAmounts third,
        out ResourceAmounts result)
    {
        if (ResourceAmounts.TryAdd(first, second, out var partial))
            return ResourceAmounts.TryAdd(partial, third, out result);
        result = default;
        return false;
    }

    private static ulong Fingerprint(RewardProposal proposal)
    {
        var hash = Fingerprint("reward", proposal.RunId);
        hash = StableHash.Add(hash, proposal.EnvironmentId);
        hash = StableHash.Add(hash, proposal.Succeeded ? 1UL : 0UL);
        foreach (var value in new[]
                 {
                     proposal.Earned.Ferrite, proposal.Earned.Lumen, proposal.Earned.DataCores,
                     proposal.Banked.Ferrite, proposal.Banked.Lumen, proposal.Banked.DataCores,
                     proposal.Retained.Ferrite, proposal.Retained.Lumen, proposal.Retained.DataCores,
                     proposal.Lost.Ferrite, proposal.Lost.Lumen, proposal.Lost.DataCores,
                     proposal.CounterDelta.Extractions, proposal.CounterDelta.NormalKills,
                     proposal.CounterDelta.EliteKills, proposal.CounterDelta.FerriteCollected,
                     proposal.CounterDelta.ResourceCellsBroken, proposal.CounterDelta.IonVeilExtractions
                 })
            hash = StableHash.Add(hash, unchecked((ulong)value));
        return hash;
    }

    private static ulong Fingerprint(string operation, string? value)
    {
        var hash = StableHash.Add(StableHash.Offset, operation);
        return StableHash.Add(hash, value ?? string.Empty);
    }

    private static string Safe(string? value) =>
        value is null ? "<null>" : value.Length <= 128 ? value : value[..128];

    private static ProfileMutationResult Applied(string code, string message) =>
        new(ProfileMutationStatus.Applied, code, message);

    private static ProfileMutationResult Rejected(string code, string message) =>
        new(ProfileMutationStatus.Rejected, code, message);
}
