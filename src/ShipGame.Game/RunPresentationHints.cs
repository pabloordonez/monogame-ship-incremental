namespace ShipGame.Game;

public readonly record struct RunPresentationHints(
    System.Numerics.Vector2 MoveIntent,
    System.Numerics.Vector2 AimDirection,
    System.Numerics.Vector2 MouseVirtual,
    bool ShowCursor,
    bool ShowAimReticle,
    bool FlashesEnabled,
    float MaxHull,
    float MaxShield,
    bool FireHeld,
    bool MineHeld);
