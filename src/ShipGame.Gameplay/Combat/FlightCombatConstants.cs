using System.Numerics;
using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Gameplay;

public static class FlightCombatConstants
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;
    public const short CommandScale = 10_000;
    public const int MaximumEntities = 2_048;
    public const int MaximumEventsPerTick = 4_096;
    public const int MaximumDamageRequestsPerTick = 4_096;
    /// <summary>Inclusive future span accepted by <see cref="FlightCombatWorld.Queue"/> (current tick + this many).</summary>
    public const int CommandHorizonTicks = TickRate * 10;
    public const int CommandSlotCount = CommandHorizonTicks + 1;
}
