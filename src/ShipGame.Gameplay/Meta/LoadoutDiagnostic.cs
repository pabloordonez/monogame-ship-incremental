using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record LoadoutDiagnostic(
    ModuleSlot Slot,
    string RequestedId,
    string EffectiveId,
    string Code,
    string Message);
