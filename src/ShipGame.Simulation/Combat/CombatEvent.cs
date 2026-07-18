using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public readonly record struct CombatEvent(
    CombatEventKind Kind,
    long Tick,
    EntityId Entity,
    EntityId Other,
    ContentId ContentId,
    Vector2 Position,
    float Amount,
    float Remaining,
    string Detail)
{
    public static CombatEvent Create(
        CombatEventKind kind,
        long tick,
        EntityId entity = default,
        EntityId other = default,
        ContentId contentId = default,
        Vector2 position = default,
        float amount = 0,
        float remaining = 0,
        string detail = "") =>
        new(kind, tick, entity, other, contentId, position, amount, remaining, detail);
}
