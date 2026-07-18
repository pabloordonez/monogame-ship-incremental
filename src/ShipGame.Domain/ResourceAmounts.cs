namespace ShipGame.Domain;

public readonly record struct ResourceAmounts(long Ferrite, long Lumen, long DataCores)
{
    public static ResourceAmounts Zero => new(0, 0, 0);

    public bool IsValid => Ferrite >= 0 && Lumen >= 0 && DataCores >= 0;

    public static bool TryAdd(ResourceAmounts left, ResourceAmounts right, out ResourceAmounts result)
    {
        try
        {
            result = new(
                checked(left.Ferrite + right.Ferrite),
                checked(left.Lumen + right.Lumen),
                checked(left.DataCores + right.DataCores));
            return result.IsValid;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }

    public static bool TrySubtract(ResourceAmounts balance, ResourceAmounts cost, out ResourceAmounts result)
    {
        if (!balance.IsValid || !cost.IsValid ||
            balance.Ferrite < cost.Ferrite ||
            balance.Lumen < cost.Lumen ||
            balance.DataCores < cost.DataCores)
        {
            result = default;
            return false;
        }

        result = new(
            balance.Ferrite - cost.Ferrite,
            balance.Lumen - cost.Lumen,
            balance.DataCores - cost.DataCores);
        return true;
    }
}
