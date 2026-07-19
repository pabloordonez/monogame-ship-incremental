using System.Numerics;
using Microsoft.Xna.Framework.Input;
using ShipGame.Ecs;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public sealed class CombatPresentationBinding
{
    private readonly List<CombatCue> _cues = new(256);
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<CombatCue> _view;

    public CombatPresentationBinding() => _view = _cues.AsReadOnly();

    public IReadOnlyList<CombatCue> Translate(
        IReadOnlyList<CombatEvent> events,
        Func<EntityId, CombatSnapshot?> snapshot)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(snapshot);
        _cues.Clear();
        for (var i = 0; i < events.Count; i++)
        {
            var value = events[i];
            var entitySnapshot = snapshot(value.Entity);
            var position = value.Position != default
                ? value.Position
                : entitySnapshot?.Position ?? Vector2.Zero;
            var (kind, asset) = value.Kind switch
            {
                CombatEventKind.WeaponFired => (CombatCueKind.Weapon, "sfx/combat/weapon"),
                CombatEventKind.CollisionDetected => (CombatCueKind.Impact, "sfx/combat/impact"),
                CombatEventKind.ShieldDamaged => (CombatCueKind.Shield, "sfx/combat/shield-hit"),
                CombatEventKind.ShieldDepleted => (CombatCueKind.ShieldBreak, "sfx/combat/shield-break"),
                CombatEventKind.HullDamaged => (CombatCueKind.Hull, "sfx/combat/hull-hit"),
                CombatEventKind.EntityDestroyed => (CombatCueKind.Destruction, "sfx/combat/destruction"),
                CombatEventKind.AbilityActivated => (CombatCueKind.Mobility, "sfx/combat/mobility"),
                CombatEventKind.AbilityRejected or CombatEventKind.CommandRejected =>
                    (CombatCueKind.Rejected, "sfx/ui/rejected"),
                CombatEventKind.EnemySpawned => (CombatCueKind.Spawn, "sfx/combat/spawn"),
                CombatEventKind.EliteActivated => (CombatCueKind.Spawn, "sfx/combat/elite"),
                CombatEventKind.MineTelegraphed => (CombatCueKind.Telegraph, "sfx/combat/mine-telegraph"),
                _ => throw new ArgumentOutOfRangeException()
            };
            _cues.Add(new CombatCue(kind, value.Tick, value.Entity, position, value.Amount, asset));
        }
        return _view;
    }
}
