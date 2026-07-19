using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record ModuleDefinition(
    string Id,
    ModuleSlot Slot,
    string? RequiredResearchId,
    bool IsDefault);
