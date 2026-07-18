using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record LoadoutPreview(
    ModuleSlot Slot,
    string ModuleId,
    bool Known,
    bool Compatible,
    bool Unlocked,
    DerivedShipStatistics Current,
    DerivedShipStatistics? Proposed,
    string Explanation);
