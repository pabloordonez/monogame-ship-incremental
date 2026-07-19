using ShipGame.Domain;

namespace ShipGame.Gameplay;

public sealed record ResearchPreview(
    ResearchDefinition Definition,
    bool Purchased,
    bool PrerequisitesMet,
    bool GateMet,
    bool Affordable,
    string Explanation);
