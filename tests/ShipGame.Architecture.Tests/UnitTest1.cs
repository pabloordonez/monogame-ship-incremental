using System.Reflection;
using ShipGame.Domain;
using ShipGame.Ecs;
using ShipGame.Simulation;

namespace ShipGame.Architecture.Tests;

public class ArchitectureTests
{
    [Fact]
    public void AuthoritativeAssembliesHaveNoMonoGameReferences()
    {
        var assemblies = new[]
        {
            typeof(ContractVersions).Assembly,
            typeof(EntityId).Assembly,
            typeof(FoundationSimulation).Assembly
        };

        foreach (var assembly in assemblies)
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name?.StartsWith("MonoGame", StringComparison.Ordinal) == true ||
                             reference.Name?.StartsWith("Microsoft.Xna", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void SimulationScheduleIsExplicitAndStable()
    {
        var simulation = new FoundationSimulation(1);
        Assert.Equal(
            ["ApplyStructuralChanges", "ConsumeCommands", "SessionTransitions", "RunClock", "PublishAndHash"],
            simulation.Schedule);
    }

    [Fact]
    public void ComponentsCannotContainFrameworkObjectsOrDelegates()
    {
        var componentAssembly = typeof(EntityId).Assembly;
        var forbidden = componentAssembly.GetTypes()
            .Where(type => type.IsValueType)
            .SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType) ||
                            field.FieldType.Namespace?.StartsWith("Microsoft.Xna", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Empty(forbidden);
    }

    [Fact]
    public void DurableVersionBoundariesAreDeclared()
    {
        Assert.All(
            new[]
            {
                ContractVersions.Save,
                ContractVersions.Content,
                ContractVersions.Generation,
                ContractVersions.Rng,
                ContractVersions.Replay,
                ContractVersions.Telemetry
            },
            version => Assert.True(version > 0));
    }
}
