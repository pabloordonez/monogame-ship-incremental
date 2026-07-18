using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record UpgradeOffer(int Threshold, IReadOnlyList<ContentId> Choices);
