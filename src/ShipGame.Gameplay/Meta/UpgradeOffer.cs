using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record UpgradeOffer(int Threshold, IReadOnlyList<ContentId> Choices);
