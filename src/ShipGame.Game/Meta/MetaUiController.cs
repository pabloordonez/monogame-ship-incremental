using ShipGame.Domain;
using ShipGame.Persistence;
ousing ShipGame.Gameplay;
using ShipGame.Telemetry;

namespace ShipGame.Game;

public sealed class MetaUiController
{
    private readonly ProfileAggregate _profile;
    private MetaScreen _settingsReturnScreen = MetaScreen.Title;

    public MetaUiController(ProfileAggregate profile, bool continuedProfile = false)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Screen = continuedProfile ? MetaScreen.Station : MetaScreen.Title;
    }

    public MetaScreen Screen { get; private set; }
    public string SelectedEnvironmentId { get; private set; } = MetaContentIds.CinderBelt;

    public StationView BuildStationView()
    {
        var snapshot = _profile.Snapshot;
        var loadout = _profile.ResolveLoadout();
        return new(
            snapshot.Balances,
            snapshot.Counters,
            _profile.DeriveStatistics(),
            snapshot.PreviousRun,
            loadout.Diagnostics);
    }

    public IReadOnlyList<EnvironmentView> BuildMapView() =>
    [
        BuildEnvironment(MetaContentIds.CinderBelt),
        BuildEnvironment(MetaContentIds.IonVeil)
    ];

    public IReadOnlyList<ResearchPreview> BuildResearchView() =>
        ResearchCatalog.All.Select(node => _profile.InspectResearch(node.Id)).ToArray();

    public IReadOnlyList<UpgradePreview> BuildUpgradeView() =>
        RunUpgradeCatalog.All.Select(node => _profile.InspectUpgrade(node.Id.Value)).ToArray();

    public IReadOnlyList<LoadoutPreview> BuildLoadoutView(ModuleSlot slot) =>
        ModuleCatalog.All
            .Where(module => module.Slot == slot)
            .Select(module => _profile.InspectModule(slot, module.Id))
            .ToArray();

    public UiActionResult EnterStation()
    {
        if (Screen is not MetaScreen.Title and not MetaScreen.Summary)
            return Rejected("navigation.invalid", "Station can only be entered from title or summary.");
        Screen = MetaScreen.Station;
        return Accepted("navigation.station", "Entered station.");
    }

    public UiActionResult EnterLobby() => EnterStation();

    public UiActionResult Open(MetaScreen destination)
    {
        var accepted = Screen switch
        {
            MetaScreen.Station => destination is MetaScreen.Map or MetaScreen.Loadout or
                MetaScreen.Research or MetaScreen.Upgrades or MetaScreen.Settings,
            MetaScreen.Title => destination == MetaScreen.Settings,
            MetaScreen.Run => destination == MetaScreen.Pause,
            MetaScreen.Pause => destination == MetaScreen.Settings,
            _ => false
        };
        if (!accepted)
            return Rejected("navigation.invalid", $"Cannot open {destination} from {Screen}.");
        if (destination == MetaScreen.Settings)
            _settingsReturnScreen = Screen;
        Screen = destination;
        return Accepted("navigation.opened", $"Opened {destination}.");
    }

    public UiActionResult Back()
    {
        Screen = Screen switch
        {
            MetaScreen.Map or MetaScreen.Loadout or MetaScreen.Research or MetaScreen.Upgrades => MetaScreen.Station,
            MetaScreen.Settings => _settingsReturnScreen,
            MetaScreen.Pause => MetaScreen.Run,
            _ => Screen
        };
        return Accepted("navigation.back", $"Returned to {Screen}.");
    }

    public UiActionResult SelectEnvironment(string environmentId)
    {
        if (Screen != MetaScreen.Map)
            return Rejected("navigation.invalid", "Environment selection is only available on the map.");
        var result = _profile.ValidateDestination(environmentId);
        if (result.Status != ProfileMutationStatus.Applied)
            return Rejected(result.Code, result.Message);
        SelectedEnvironmentId = environmentId;
        return Accepted("environment.selected", $"Selected {environmentId}.");
    }

    public UiActionResult Launch()
    {
        if (Screen is not MetaScreen.Map and not MetaScreen.Station)
            return Rejected("navigation.invalid", "Launch is only available from the station or map.");
        var access = _profile.ValidateDestination(SelectedEnvironmentId);
        if (access.Status != ProfileMutationStatus.Applied)
            return Rejected(access.Code, access.Message);
        Screen = MetaScreen.Run;
        return Accepted("run.launched", $"Launching {SelectedEnvironmentId}.");
    }

    public UiActionResult ShowSummary()
    {
        if (Screen != MetaScreen.Run)
            return Rejected("navigation.invalid", "Summary requires an active run.");
        Screen = MetaScreen.Summary;
        return Accepted("summary.opened", "Run summary opened.");
    }

    public UiActionResult PurchaseResearch(string transactionId, string researchId)
    {
        if (Screen != MetaScreen.Research)
            return Rejected("navigation.invalid", "Research purchases are only available in research.");
        return FromMutation(_profile.PurchaseResearch(transactionId, researchId));
    }

    public UiActionResult PurchaseUpgrade(string transactionId, string upgradeId)
    {
        if (Screen != MetaScreen.Upgrades)
            return Rejected("navigation.invalid", "Upgrade purchases are only available in upgrades.");
        return FromMutation(_profile.PurchaseUpgrade(transactionId, upgradeId));
    }

    public UiActionResult EquipModule(string transactionId, ModuleSlot slot, string moduleId)
    {
        if (Screen != MetaScreen.Loadout)
            return Rejected("navigation.invalid", "Loadout changes are only available in loadout.");
        return FromMutation(_profile.EquipModule(transactionId, slot, moduleId));
    }

    public UiActionResult ApplySettings(string transactionId, GameSettings settings)
    {
        if (Screen != MetaScreen.Settings)
            return Rejected("navigation.invalid", "Settings can only be changed on the settings screen.");
        return FromMutation(_profile.UpdateSettings(transactionId, settings));
    }

    private EnvironmentView BuildEnvironment(string environmentId)
    {
        var result = _profile.ValidateDestination(environmentId);
        return new(
            environmentId,
            result.Status == ProfileMutationStatus.Applied,
            result.Message,
            string.Equals(environmentId, SelectedEnvironmentId, StringComparison.Ordinal));
    }

    private static UiActionResult FromMutation(ProfileMutationResult result) =>
        new(result.Status is ProfileMutationStatus.Applied or ProfileMutationStatus.Duplicate, result.Code, result.Message);

    private static UiActionResult Accepted(string code, string message) => new(true, code, message);
    private static UiActionResult Rejected(string code, string message) => new(false, code, message);
}
