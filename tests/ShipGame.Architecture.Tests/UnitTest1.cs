using System.Reflection;
using System.Xml.Linq;
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
    public void ProductionProjectFilesMatchDependencyAndPackagePolicy()
    {
        var projects = ProjectArchitecturePolicy.LoadProductionProjectXml(FindRepositoryRoot());

        Assert.Contains(projects.Keys, path =>
            Path.GetFileNameWithoutExtension(path) == "ShipGame.ContentBuilder");
        Assert.Empty(ProjectArchitecturePolicy.Validate(projects));
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
    public void ProjectPolicyRejectsInvalidXmlForbiddenEdgesCyclesAndPackagePins()
    {
        var projects = ProjectArchitecturePolicy.LoadProductionProjectXml(FindRepositoryRoot());

        var malformed = projects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        malformed[ProjectPath(malformed, "ShipGame.Domain")] =
            "<Project><ItemGroup><ProjectReference /></ItemGroup></Project>";
        Assert.Contains(
            ProjectArchitecturePolicy.Validate(malformed),
            issue => issue.Code == "project.reference-invalid");

        var malformedXml = projects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        malformedXml[ProjectPath(malformedXml, "ShipGame.Domain")] = "<Project";
        Assert.Contains(
            ProjectArchitecturePolicy.Validate(malformedXml),
            issue => issue.Code == "project.xml-invalid");

        var cyclic = AddProjectReference(projects, "ShipGame.Domain", "ShipGame.Ecs");
        var cycleIssues = ProjectArchitecturePolicy.Validate(cyclic);
        Assert.Contains(cycleIssues, issue => issue.Code == "project.reference-forbidden");
        Assert.Contains(cycleIssues, issue => issue.Code == "project.cycle");

        var badPin = ChangePackageVersion(
            projects,
            "ShipGame.ContentBuilder",
            "MonoGame.Framework.Content.Pipeline",
            "3.8.4");
        Assert.Contains(
            ProjectArchitecturePolicy.Validate(badPin),
            issue => issue.Code == "package.version");

        var badPlacement = AddPackage(
            projects,
            "ShipGame.Domain",
            "MonoGame.Framework.Native",
            "3.8.5");
        Assert.Contains(
            ProjectArchitecturePolicy.Validate(badPlacement),
            issue => issue.Code == "package.forbidden");
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

    private static Dictionary<string, string> AddProjectReference(
        IReadOnlyDictionary<string, string> projects,
        string from,
        string to)
    {
        var changed = projects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var fromPath = ProjectPath(changed, from);
        var toPath = ProjectPath(changed, to);
        var document = XDocument.Parse(changed[fromPath]);
        document.Root!.Add(
            new XElement(
                "ItemGroup",
                new XElement(
                    "ProjectReference",
                    new XAttribute("Include", Path.GetRelativePath(Path.GetDirectoryName(fromPath)!, toPath)))));
        changed[fromPath] = document.ToString();
        return changed;
    }

    private static Dictionary<string, string> ChangePackageVersion(
        IReadOnlyDictionary<string, string> projects,
        string project,
        string package,
        string version)
    {
        var changed = projects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var path = ProjectPath(changed, project);
        var document = XDocument.Parse(changed[path]);
        var reference = document.Descendants("PackageReference")
            .Single(element => element.Attribute("Include")?.Value == package);
        reference.SetAttributeValue("Version", version);
        changed[path] = document.ToString();
        return changed;
    }

    private static Dictionary<string, string> AddPackage(
        IReadOnlyDictionary<string, string> projects,
        string project,
        string package,
        string version)
    {
        var changed = projects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var path = ProjectPath(changed, project);
        var document = XDocument.Parse(changed[path]);
        document.Root!.Add(
            new XElement(
                "ItemGroup",
                new XElement(
                    "PackageReference",
                    new XAttribute("Include", package),
                    new XAttribute("Version", version))));
        changed[path] = document.ToString();
        return changed;
    }

    private static string ProjectPath(IReadOnlyDictionary<string, string> projects, string name) =>
        projects.Keys.Single(path => Path.GetFileNameWithoutExtension(path) == name);

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
