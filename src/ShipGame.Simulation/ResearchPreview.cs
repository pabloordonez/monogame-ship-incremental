using ShipGame.Domain;

namespace ShipGame.Simulation;

public sealed record ResearchPreview(
    ResearchDefinition Definition,
    bool Purchased,
    bool PrerequisitesMet,
    bool GateMet,
    bool Affordable,
    string Explanation);
