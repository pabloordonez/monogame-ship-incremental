namespace ShipGame.Game.Tests;

public sealed class ParticleSystemTests
{
    [Fact]
    public void Burst_RespectsCapacity()
    {
        var system = new ParticleSystem();
        for (var i = 0; i < 20; i++)
            system.Burst(default, ParticlePresets.Destruction);

        Assert.True(system.ActiveCount <= ParticleSystem.Capacity);
        Assert.Equal(ParticleSystem.Capacity, system.ActiveCount);
    }

    [Fact]
    public void Update_ExpiresParticles()
    {
        var system = new ParticleSystem();
        system.Burst(
            default,
            new ParticleBurst(
                Count: 8,
                MinSpeed: 0f,
                MaxSpeed: 0f,
                MinLife: 0.05f,
                MaxLife: 0.05f,
                ColorA: Microsoft.Xna.Framework.Color.White,
                ColorB: Microsoft.Xna.Framework.Color.White,
                MinSize: 1,
                MaxSize: 1));

        Assert.Equal(8, system.ActiveCount);
        system.Update(0.06f);
        Assert.Equal(0, system.ActiveCount);
    }
}
