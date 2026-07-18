using ShipGame.Domain;
using ShipGame.Simulation;

namespace ShipGame.Game;

public readonly record struct WorldRunPresentationCue(
    ContentId AssetId,
    ContentId AudioCueId,
    int Amount);
