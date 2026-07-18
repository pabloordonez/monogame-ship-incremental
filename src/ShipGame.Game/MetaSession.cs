using ShipGame.Domain;
using ShipGame.Persistence;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Game;

/// <summary>
/// Composes profile, screen model, versioned meta saves, and consent-aware telemetry
/// for the P5 DesktopVK host loop.
/// </summary>
public sealed class MetaSession : IDisposable
{
    public const string CatalogFingerprint = "foundation-catalog-v1";
    public const ulong DefaultNewProfileSeed = 0x5348495047414D45UL;

    private readonly MetaSaveRepository _saves;
    private readonly ConsentAwareTelemetry _telemetry;
    private readonly MetaContentCompatibility _knownContent;
    private ProfileAggregate _profile;
    private MetaUiController _ui;
    private long _elapsedMilliseconds;
    private bool _allowOverwriteUnrecoverable;

    public MetaSession(
        string saveDirectory,
        Func<ITelemetrySink>? sinkFactory = null,
        ulong? newProfileSeed = null)
    {
        _saves = new MetaSaveRepository(saveDirectory);
        _knownContent = CreateKnownContent();
        var loaded = _saves.Load(CatalogFingerprint, _knownContent);
        LoadStatus = loaded.Status;
        LoadDiagnostics = loaded.Diagnostics ?? [];
        RecoveredFromBackup = loaded.RecoveredFromBackup;
        MigratedOnLoad = loaded.Migrated;

        var continued = loaded.Status == CompatibilityStatus.Supported && loaded.Profile is not null;
        RequiresExplicitNewProfile = loaded.Status is
            CompatibilityStatus.Corrupt or
            CompatibilityStatus.IncompatibleNewer or
            CompatibilityStatus.MissingContent;

        if (continued)
            _profile = new ProfileAggregate(loaded.Profile!);
        else if (loaded.Status == CompatibilityStatus.Missing)
            _profile = ProfileAggregate.CreateNew(newProfileSeed ?? DefaultNewProfileSeed);
        else
        {
            // Unrecoverable durable state: keep a placeholder for UI inspection but refuse silent overwrite.
            _profile = ProfileAggregate.CreateNew(newProfileSeed ?? DefaultNewProfileSeed);
        }

        _ui = new MetaUiController(_profile, continued);
        _telemetry = new ConsentAwareTelemetry(
            _profile.Snapshot.Settings.TelemetryConsent,
            sinkFactory ?? (() => new DisabledTelemetrySink()));
        Record(
            continued ? MetaTelemetryFactKind.ContinueProfile : MetaTelemetryFactKind.NewProfile,
            succeeded: continued || loaded.Status == CompatibilityStatus.Missing);
        if (loaded.RecoveredFromBackup)
            Record(MetaTelemetryFactKind.SaveRecovered);
        Record(MetaTelemetryFactKind.ScreenEntered, subjectCode: (int)_ui.Screen);
    }

    public ProfileAggregate Profile => _profile;
    public MetaUiController Ui => _ui;
    public MetaScreen Screen => _ui.Screen;
    public ConsentAwareTelemetry Telemetry => _telemetry;
    public CompatibilityStatus LoadStatus { get; private set; }
    public IReadOnlyList<string> LoadDiagnostics { get; private set; }
    public bool RecoveredFromBackup { get; private set; }
    public bool MigratedOnLoad { get; private set; }

    /// <summary>
    /// True when durable saves are corrupt/incompatible/missing-content.
    /// Mutations and persist are blocked until <see cref="CreateNewProfile"/> is called.
    /// </summary>
    public bool RequiresExplicitNewProfile { get; private set; }

    public StationView Station => _ui.BuildStationView();
    public LobbyView Lobby => _ui.BuildLobbyView();
    public IReadOnlyList<EnvironmentView> Map => _ui.BuildMapView();
    public IReadOnlyList<ResearchPreview> Research => _ui.BuildResearchView();
    public IReadOnlyList<UpgradePreview> Upgrades => _ui.BuildUpgradeView();

    public UiActionResult CreateNewProfile(ulong? seed = null)
    {
        if (!RequiresExplicitNewProfile && LoadStatus != CompatibilityStatus.Missing)
            return Rejected(
                "profile.new-not-required",
                "An explicit new profile is only required when continue state is unrecoverable.");

        _allowOverwriteUnrecoverable = true;
        _profile = ProfileAggregate.CreateNew(seed ?? DefaultNewProfileSeed);
        _ui = new MetaUiController(_profile, continuedProfile: false);
        _telemetry.SetConsent(_profile.Snapshot.Settings.TelemetryConsent);
        if (!TryPersist("new-profile"))
        {
            RequiresExplicitNewProfile = true;
            _allowOverwriteUnrecoverable = false;
            return Rejected("save.failed", "Could not create a durable new profile.");
        }

        RequiresExplicitNewProfile = false;
        LoadStatus = CompatibilityStatus.Supported;
        LoadDiagnostics = [];
        Record(MetaTelemetryFactKind.NewProfile);
        Record(MetaTelemetryFactKind.ScreenEntered, subjectCode: (int)_ui.Screen);
        return Accepted("profile.created", "Created a new profile after explicit confirmation.");
    }

    public UiActionResult Navigate(MetaScreen destination)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var result = destination == MetaScreen.Station
            ? _ui.EnterStation()
            : _ui.Open(destination);
        if (result.Accepted)
            Record(MetaTelemetryFactKind.ScreenEntered, subjectCode: (int)_ui.Screen);
        return result;
    }

    public UiActionResult Back()
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var result = _ui.Back();
        if (result.Accepted)
            Record(MetaTelemetryFactKind.ScreenEntered, subjectCode: (int)_ui.Screen);
        return result;
    }

    public UiActionResult SelectEnvironment(string environmentId)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var result = _ui.SelectEnvironment(environmentId);
        if (result.Accepted)
            Record(MetaTelemetryFactKind.EnvironmentSelected, subjectCode: environmentId.GetHashCode());
        else if (_ui.Screen == MetaScreen.Map)
            Record(MetaTelemetryFactKind.LockInspected, subjectCode: environmentId.GetHashCode(), succeeded: false);
        return result;
    }

    public UiActionResult PurchaseResearch(string transactionId, string researchId)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        Record(MetaTelemetryFactKind.ResearchViewed, subjectCode: researchId.GetHashCode());
        var prior = _profile.Snapshot;
        var result = _ui.PurchaseResearch(transactionId, researchId);
        if (result.Accepted && !TryPersist("research"))
        {
            _profile.Restore(prior);
            result = Rejected("save.failed", "Research purchase could not be persisted.");
        }

        Record(
            result.Accepted ? MetaTelemetryFactKind.ResearchPurchased : MetaTelemetryFactKind.ResearchRejected,
            subjectCode: researchId.GetHashCode(),
            succeeded: result.Accepted);
        return result;
    }

    public UiActionResult PurchaseUpgrade(string transactionId, string upgradeId)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var prior = _profile.Snapshot;
        var result = _ui.PurchaseUpgrade(transactionId, upgradeId);
        if (result.Accepted && !TryPersist("upgrade"))
        {
            _profile.Restore(prior);
            return Rejected("save.failed", "Upgrade purchase could not be persisted.");
        }

        return result;
    }

    public UiActionResult EquipModule(string transactionId, ModuleSlot slot, string moduleId)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var prior = _profile.Snapshot;
        var result = _ui.EquipModule(transactionId, slot, moduleId);
        if (result.Accepted && !TryPersist("loadout"))
        {
            _profile.Restore(prior);
            return Rejected("save.failed", "Loadout change could not be persisted.");
        }

        if (result.Accepted)
            Record(MetaTelemetryFactKind.LoadoutChanged, subjectCode: (int)slot);
        return result;
    }

    public UiActionResult ApplySettings(string transactionId, GameSettings settings)
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        var prior = _profile.Snapshot;
        var priorConsent = _telemetry;
        var result = _ui.ApplySettings(transactionId, settings);
        if (result.Accepted)
        {
            _telemetry.SetConsent(settings.TelemetryConsent);
            if (!TryPersist("settings"))
            {
                _profile.Restore(prior);
                _telemetry.SetConsent(prior.Settings.TelemetryConsent);
                return Rejected("save.failed", "Settings change could not be persisted.");
            }

            Record(MetaTelemetryFactKind.OptionChanged);
        }

        _ = priorConsent;
        return result;
    }

    public UiActionResult Launch()
    {
        if (RequiresExplicitNewProfile)
            return RejectedUnrecoverable();
        if (_ui.Screen is not MetaScreen.Map and not MetaScreen.Station)
            return Rejected("navigation.invalid", "Launch is only available from the station or map.");
        var access = _profile.ValidateDestination(_ui.SelectedEnvironmentId);
        if (access.Status != ProfileMutationStatus.Applied)
            return Rejected(access.Code, access.Message);

        var prior = _profile.Snapshot;
        var begin = _profile.BeginRun($"TX_BEGIN_RUN_{prior.RunIndex + 1}");
        if (begin.Status is not ProfileMutationStatus.Applied and not ProfileMutationStatus.Duplicate)
            return Rejected(begin.Code, begin.Message);
        if (!TryPersist("launch"))
        {
            _profile.Restore(prior);
            return Rejected("save.failed", "Launch could not persist the locked run index.");
        }

        var result = _ui.Launch();
        if (!result.Accepted)
        {
            // Screen mutation failed after durable lock; keep index (run was reserved) but report failure.
            return result;
        }

        Record(MetaTelemetryFactKind.RunStarted, subjectCode: _ui.SelectedEnvironmentId.GetHashCode());
        return result;
    }

    public ProfileMutationResult CommitReward(RewardProposal proposal)
    {
        if (RequiresExplicitNewProfile)
            return new(ProfileMutationStatus.Rejected, "profile.unrecoverable", UnrecoverableMessage);

        var prior = _profile.Snapshot;
        var result = _profile.CommitAcceptedReward(proposal);
        if (result.Status == ProfileMutationStatus.Applied)
        {
            if (!TryPersist("summary"))
            {
                _profile.Restore(prior);
                return new(ProfileMutationStatus.Rejected, "save.failed", "Reward commit could not be persisted.");
            }

            Record(MetaTelemetryFactKind.RunResolved, succeeded: proposal.Succeeded, amount: proposal.Earned.Ferrite);
            if (_ui.Screen == MetaScreen.Run)
                _ui.ShowSummary();
        }
        else if (result.Status == ProfileMutationStatus.Duplicate)
        {
            Record(MetaTelemetryFactKind.RunResolved, succeeded: proposal.Succeeded, amount: proposal.Earned.Ferrite);
            if (_ui.Screen == MetaScreen.Run)
                _ui.ShowSummary();
        }

        return result;
    }

    public MetaSaveLoadResult ContinueFromDisk()
    {
        var loaded = _saves.Load(CatalogFingerprint, _knownContent);
        LoadStatus = loaded.Status;
        LoadDiagnostics = loaded.Diagnostics ?? [];
        RecoveredFromBackup = loaded.RecoveredFromBackup;
        MigratedOnLoad = loaded.Migrated;
        RequiresExplicitNewProfile = loaded.Status is
            CompatibilityStatus.Corrupt or
            CompatibilityStatus.IncompatibleNewer or
            CompatibilityStatus.MissingContent;
        if (loaded.Status != CompatibilityStatus.Supported || loaded.Profile is null)
            return loaded;
        _profile = new ProfileAggregate(loaded.Profile);
        _ui = new MetaUiController(_profile, continuedProfile: true);
        _telemetry.SetConsent(_profile.Snapshot.Settings.TelemetryConsent);
        RequiresExplicitNewProfile = false;
        Record(MetaTelemetryFactKind.ContinueProfile);
        Record(MetaTelemetryFactKind.ScreenEntered, subjectCode: (int)_ui.Screen);
        return loaded;
    }

    public bool TryPersist(string reason)
    {
        if (RequiresExplicitNewProfile && !_allowOverwriteUnrecoverable)
        {
            Record(MetaTelemetryFactKind.SaveFailed, subjectCode: reason.GetHashCode(), succeeded: false);
            return false;
        }

        Record(MetaTelemetryFactKind.SaveStarted, subjectCode: reason.GetHashCode());
        try
        {
            var envelope = _saves.CreateEnvelope(_profile.Snapshot, "P4_META_UI", CatalogFingerprint);
            _saves.Write(envelope);
            Record(MetaTelemetryFactKind.SaveSucceeded, subjectCode: reason.GetHashCode());
            _allowOverwriteUnrecoverable = false;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Record(MetaTelemetryFactKind.SaveFailed, subjectCode: reason.GetHashCode(), succeeded: false);
            return false;
        }
    }

    public void Persist(string reason) => TryPersist(reason);

    public void AdvanceClock(long milliseconds)
    {
        if (milliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        _elapsedMilliseconds = checked(_elapsedMilliseconds + milliseconds);
    }

    public void Dispose() => _telemetry.Dispose();

    private const string UnrecoverableMessage =
        "Durable profile is unrecoverable. Call CreateNewProfile after explicit confirmation.";

    private static UiActionResult RejectedUnrecoverable() =>
        Rejected("profile.unrecoverable", UnrecoverableMessage);

    private static UiActionResult Accepted(string code, string message) => new(true, code, message);
    private static UiActionResult Rejected(string code, string message) => new(false, code, message);

    private void Record(
        MetaTelemetryFactKind kind,
        int subjectCode = 0,
        long amount = 0,
        bool succeeded = true)
    {
        var snapshot = _profile.Snapshot;
        _telemetry.Record(
            new MetaTelemetryFact(kind, subjectCode, amount, succeeded),
            new MetaTelemetryContext(
                snapshot.ProfileSeed,
                snapshot.ProfileSeed ^ 0x5E55UL,
                (ulong)Math.Max(0, snapshot.RunIndex),
                4,
                ContractVersions.Content,
                ContractVersions.Generation,
                _elapsedMilliseconds));
    }

    private static MetaContentCompatibility CreateKnownContent() =>
        new(
            ResearchCatalog.All.Select(node => node.Id).ToHashSet(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal)
            {
                MetaContentIds.CinderBelt,
                MetaContentIds.IonVeil
            },
            ModuleCatalog.All.Select(module => module.Id).ToHashSet(StringComparer.Ordinal),
            RunUpgradeCatalog.All.Select(upgrade => upgrade.Id.Value).ToHashSet(StringComparer.Ordinal));
}
