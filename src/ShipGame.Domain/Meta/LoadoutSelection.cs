namespace ShipGame.Domain;

public sealed record LoadoutSelection(
    string Weapon,
    string Mining,
    string Shield,
    string Engine,
    string Utility)
{
    public string For(ModuleSlot slot) => slot switch
    {
        ModuleSlot.Weapon => Weapon,
        ModuleSlot.Mining => Mining,
        ModuleSlot.Shield => Shield,
        ModuleSlot.Engine => Engine,
        ModuleSlot.Utility => Utility,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public LoadoutSelection With(ModuleSlot slot, string moduleId) => slot switch
    {
        ModuleSlot.Weapon => this with { Weapon = moduleId },
        ModuleSlot.Mining => this with { Mining = moduleId },
        ModuleSlot.Shield => this with { Shield = moduleId },
        ModuleSlot.Engine => this with { Engine = moduleId },
        ModuleSlot.Utility => this with { Utility = moduleId },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
