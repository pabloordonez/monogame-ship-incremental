using System.Reflection;
using ShipGame.Content;
using ShipGame.Domain;
using ShipGame.Ecs;
using ShipGame.Game;
using ShipGame.Persistence;
using ShipGame.Simulation;
using ShipGame.Telemetry;

namespace ShipGame.Architecture.Tests;

public class ArchitectureTests
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedDependencies =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ShipGame.Domain"] = [],
            ["ShipGame.Ecs"] = ["ShipGame.Domain"],
            ["ShipGame.Content"] = ["ShipGame.Domain"],
            ["ShipGame.Simulation"] = ["ShipGame.Domain", "ShipGame.Ecs", "ShipGame.Content"],
            ["ShipGame.Persistence"] = ["ShipGame.Domain", "ShipGame.Content"],
            ["ShipGame.Telemetry"] = ["ShipGame.Domain"],
            ["ShipGame.Game"] =
                ["ShipGame.Domain", "ShipGame.Simulation", "ShipGame.Content", "ShipGame.Persistence", "ShipGame.Telemetry"]
        };

    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(ContractVersions).Assembly,
        typeof(EntityId).Assembly,
        typeof(ContentDefinition).Assembly,
        typeof(FoundationSimulation).Assembly,
        typeof(SaveRepository).Assembly,
        typeof(TelemetryRecord).Assembly,
        typeof(ShipGameHost).Assembly
    ];

    [Fact]
    public void ProductionAssemblyGraphMatchesPolicyAndHasNoCycles()
    {
        var graph = ProductionAssemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(AllowedDependencies.ContainsKey)
                .ToArray(),
            StringComparer.Ordinal);

        Assert.Equal(AllowedDependencies.Keys.Order(), graph.Keys.Order());
        foreach (var (project, references) in graph)
            Assert.All(references, reference => Assert.Contains(reference, AllowedDependencies[project]));
        Assert.False(HasCycle(graph));
    }

    [Fact]
    public void OnlyGameReferencesMonoGameAndAuthorityAvoidsForbiddenApis()
    {
        foreach (var assembly in ProductionAssemblies)
        {
            var hasMonoGame = assembly.GetReferencedAssemblies().Any(
                reference => reference.Name?.StartsWith("MonoGame", StringComparison.Ordinal) == true ||
                             reference.Name?.StartsWith("Microsoft.Xna", StringComparison.Ordinal) == true);
            Assert.Equal(assembly == typeof(ShipGameHost).Assembly, hasMonoGame);
        }

        var root = FindRepositoryRoot();
        var authority = new[]
        {
            Path.Combine(root, "src", "ShipGame.Domain"),
            Path.Combine(root, "src", "ShipGame.Ecs"),
            Path.Combine(root, "src", "ShipGame.Simulation")
        };
        var forbidden = new[]
        {
            "Microsoft.Xna", "MonoGame", "System.IO", "System.Text.Json", "DateTime",
            "Environment.", "Guid.", "new Random(", "Random.Shared"
        };
        foreach (var file in authority.SelectMany(path => Directory.EnumerateFiles(path, "*.cs")))
            Assert.Empty(FindForbidden(File.ReadAllText(file), forbidden));

        Assert.NotEmpty(FindForbidden("var rng = new Random();", forbidden));
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
        var forbidden = new[] { typeof(EntityId).Assembly, typeof(FoundationSimulation).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsValueType)
            .SelectMany(type => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType) ||
                            typeof(IDisposable).IsAssignableFrom(field.FieldType) ||
                            field.FieldType.Namespace?.StartsWith("Microsoft.Xna", StringComparison.Ordinal) == true ||
                            field.FieldType.Namespace?.StartsWith("MonoGame", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Empty(forbidden);

        Assert.Contains(
            typeof(BadComponent).GetFields(),
            field => typeof(Delegate).IsAssignableFrom(field.FieldType));
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

        var durableProperties = typeof(DurableVersions).GetProperties()
            .Where(property => property.PropertyType == typeof(int))
            .Select(property => property.Name)
            .Order()
            .ToArray();
        Assert.Equal(["Content", "Generation", "Replay", "Rng", "Save", "Telemetry"], durableProperties);

        var registry = new SaveMigrationRegistry();
        Assert.True(IsContiguous(registry.SupportedSaveVersions));
        Assert.False(IsContiguous([1, 3]));
    }

    [Fact]
    public void RuntimeDefinitionsUseTypedIds()
    {
        Assert.Equal(typeof(ContentId), typeof(ContentDefinition).GetProperty(nameof(ContentDefinition.Id))!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<ContentId>),
            Nullable.GetUnderlyingType(typeof(ContentDefinition).GetProperty(nameof(ContentDefinition.References))!.PropertyType)
            ?? typeof(ContentDefinition).GetProperty(nameof(ContentDefinition.References))!.PropertyType);
    }

    [Fact]
    public void HeadlessAuthorityInitializesAndTicksWithoutGraphics()
    {
        var simulation = new FoundationSimulation(1, 2);
        simulation.Queue(new CommandFrame(0, Confirm: true));

        var exception = Record.Exception(() => simulation.Step());

        Assert.Null(exception);
        Assert.Equal(AppState.Lobby, simulation.State);
    }

    [Fact]
    public void ArchitecturePoliciesRejectRepresentativeGraphViolations()
    {
        var cycle = new Dictionary<string, string[]>
        {
            ["A"] = ["B"],
            ["B"] = ["A"]
        };
        Assert.True(HasCycle(cycle));

        var invalidDependency = new Dictionary<string, string[]>
        {
            ["ShipGame.Domain"] = ["ShipGame.Game"]
        };
        Assert.Contains(
            invalidDependency["ShipGame.Domain"],
            dependency => !AllowedDependencies["ShipGame.Domain"].Contains(dependency));
    }

    private static string[] FindForbidden(string source, IEnumerable<string> forbidden) =>
        forbidden.Where(token => source.Contains(token, StringComparison.Ordinal)).ToArray();

    private static bool IsContiguous(IReadOnlyList<int> versions)
    {
        if (versions.Count == 0)
            return false;
        var ordered = versions.Order().ToArray();
        return ordered.Zip(ordered.Skip(1), (left, right) => right == left + 1).All(value => value);
    }

    private static bool HasCycle(IReadOnlyDictionary<string, string[]> graph)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string node)
        {
            if (!visiting.Add(node))
                return true;
            if (visited.Contains(node))
            {
                visiting.Remove(node);
                return false;
            }
            foreach (var dependency in graph.GetValueOrDefault(node, []))
                if (graph.ContainsKey(dependency) && Visit(dependency))
                    return true;
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }
        return graph.Keys.Any(Visit);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ShipGame.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate ShipGame.sln.");
    }

    public struct BadComponent
    {
        public Action? Callback;
    }
}
