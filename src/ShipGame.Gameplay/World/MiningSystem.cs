using ShipGame.Domain;
using ShipGame.Ecs;

namespace ShipGame.Simulation;

public sealed class MiningSystem
{
    public IReadOnlyList<CellBrokenFact> Resolve(World world, IEnumerable<MiningContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(contacts);
        var materialized = contacts.Take(4097).ToArray();
        if (materialized.Length > 4096)
            throw new ArgumentException("Mining contact limit exceeded.", nameof(contacts));
        var broken = new List<CellBrokenFact>();
        foreach (var contact in materialized.OrderBy(contact => contact.Cell).ThenBy(contact => contact.Source))
        {
            if (contact.Damage <= 0 || !world.IsAlive(contact.Cell) || !world.Store<MineableCell>().Has(contact.Cell))
                continue;
            ref var cell = ref world.Get<MineableCell>(contact.Cell);
            if (cell.Broken)
                continue;
            cell.Health = Math.Max(0, cell.Health - Math.Min(contact.Damage, 100_000));
            if (cell.Health != 0)
                continue;
            cell.Broken = true;
            broken.Add(new(contact.Cell, cell.CellId, cell.Kind));
        }
        return broken;
    }
}
