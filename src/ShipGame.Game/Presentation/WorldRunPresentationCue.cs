using ShipGame.Domain;
using ShipGame.Gameplay;

namespace ShipGame.Game;

public readonly record struct WorldRunPresentationCue(
    ContentId AssetId,
    ContentId AudioCueId,
    int Amount);
