namespace ShipGame.Domain;

public readonly record struct LifetimeCounters(
    long Extractions,
    long NormalKills,
    long EliteKills,
    long FerriteCollected,
    long ResourceCellsBroken,
    long IonVeilExtractions)
{
    public static LifetimeCounters Zero => new(0, 0, 0, 0, 0, 0);

    public bool IsValid =>
        Extractions >= 0 &&
        NormalKills >= 0 &&
        EliteKills >= 0 &&
        FerriteCollected >= 0 &&
        ResourceCellsBroken >= 0 &&
        IonVeilExtractions >= 0;

    public static bool TryAdd(LifetimeCounters left, LifetimeCounters right, out LifetimeCounters result)
    {
        try
        {
            result = new(
                checked(left.Extractions + right.Extractions),
                checked(left.NormalKills + right.NormalKills),
                checked(left.EliteKills + right.EliteKills),
                checked(left.FerriteCollected + right.FerriteCollected),
                checked(left.ResourceCellsBroken + right.ResourceCellsBroken),
                checked(left.IonVeilExtractions + right.IonVeilExtractions));
            return result.IsValid;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }
}
