using System.Xml.Linq;

namespace ShipGame.Architecture.Tests;

internal static class ProjectArchitecturePolicy
{
    private const string MonoGameVersion = "3.8.5";

    private static readonly IReadOnlyDictionary<string, string[]> AllowedProjectReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["ShipGame.Domain"] = [],
            ["ShipGame.Ecs"] = ["ShipGame.Domain"],
            ["ShipGame.Content"] = ["ShipGame.Domain"],
            ["ShipGame.Gameplay"] = ["ShipGame.Domain", "ShipGame.Ecs", "ShipGame.Content"],
            ["ShipGame.Persistence"] = ["ShipGame.Domain", "ShipGame.Content"],
            ["ShipGame.Telemetry"] = ["ShipGame.Domain"],
            ["ShipGame.Game"] =
                ["ShipGame.Domain", "ShipGame.Gameplay", "ShipGame.Content", "ShipGame.Persistence", "ShipGame.Telemetry"],
            ["ShipGame.ContentBuilder"] = ["ShipGame.Content"]
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> AllowedPackages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["ShipGame.Domain"] = Packages(),
            ["ShipGame.Ecs"] = Packages(),
            ["ShipGame.Content"] = Packages(),
            ["ShipGame.Gameplay"] = Packages(),
            ["ShipGame.Persistence"] = Packages(),
            ["ShipGame.Telemetry"] = Packages(),
            ["ShipGame.Game"] = Packages(
                ("MonoGame.Framework.Native", MonoGameVersion),
                ("MonoGame.Runtime.Windows.Vulkan", MonoGameVersion)),
            ["ShipGame.ContentBuilder"] = Packages(
                ("MonoGame.Framework.Content.Pipeline", MonoGameVersion),
                ("MonoGame.Framework.Native", MonoGameVersion))
        };

    public static IReadOnlyDictionary<string, string> LoadProductionProjectXml(string repositoryRoot)
    {
        var projectPaths = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "tools", "ShipGame.ContentBuilder"),
                "*.csproj",
                SearchOption.TopDirectoryOnly));
        return projectPaths.ToDictionary(
            NormalizePath,
            File.ReadAllText,
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ProjectPolicyIssue> Validate(IReadOnlyDictionary<string, string> projectFiles)
    {
        var issues = new List<ProjectPolicyIssue>();
        var projects = new Dictionary<string, ParsedProject>(StringComparer.Ordinal);
        var pathsToNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawPath, xml) in projectFiles)
        {
            var path = NormalizePath(rawPath);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!AllowedProjectReferences.ContainsKey(name))
            {
                issues.Add(new("project.unexpected", $"Unexpected production project '{name}'."));
                continue;
            }
            if (projects.ContainsKey(name))
            {
                issues.Add(new("project.duplicate", $"Duplicate production project '{name}'."));
                continue;
            }
            try
            {
                var document = XDocument.Parse(xml, LoadOptions.None);
                var references = document.Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .ToArray();
                if (references.Any(string.IsNullOrWhiteSpace))
                    issues.Add(new("project.reference-invalid", $"Project '{name}' has a ProjectReference without Include."));

                var packages = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var element in document.Descendants("PackageReference"))
                {
                    var package = element.Attribute("Include")?.Value;
                    var version = element.Attribute("Version")?.Value ?? element.Element("Version")?.Value;
                    if (string.IsNullOrWhiteSpace(package) || string.IsNullOrWhiteSpace(version))
                    {
                        issues.Add(new("package.invalid", $"Project '{name}' has an incomplete PackageReference."));
                        continue;
                    }
                    if (!packages.TryAdd(package, version))
                        issues.Add(new("package.duplicate", $"Project '{name}' repeats package '{package}'."));
                }

                projects.Add(name, new(path, references.Where(reference => !string.IsNullOrWhiteSpace(reference)).Cast<string>().ToArray(), packages));
                pathsToNames.Add(path, name);
            }
            catch (Exception exception) when (exception is System.Xml.XmlException or InvalidOperationException)
            {
                issues.Add(new("project.xml-invalid", $"Project '{name}' XML is invalid: {exception.Message}"));
            }
        }

        foreach (var required in AllowedProjectReferences.Keys)
            if (!projects.ContainsKey(required))
                issues.Add(new("project.missing", $"Required production project '{required}' is missing."));

        var graph = projects.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (name, project) in projects)
        {
            foreach (var include in project.ProjectReferences)
            {
                var targetPath = NormalizePath(Path.Combine(
                    Path.GetDirectoryName(project.Path)!,
                    NormalizeSeparators(include)));
                if (!pathsToNames.TryGetValue(targetPath, out var target))
                {
                    issues.Add(new("project.reference-unknown", $"Project '{name}' references non-production project '{include}'."));
                    continue;
                }
                graph[name].Add(target);
                if (!AllowedProjectReferences[name].Contains(target, StringComparer.Ordinal))
                    issues.Add(new("project.reference-forbidden", $"Project '{name}' cannot reference '{target}'."));
            }

            var expectedPackages = AllowedPackages[name];
            foreach (var (package, version) in project.Packages)
            {
                if (!expectedPackages.TryGetValue(package, out var expectedVersion))
                    issues.Add(new("package.forbidden", $"Project '{name}' cannot reference package '{package}'."));
                else if (!string.Equals(version, expectedVersion, StringComparison.Ordinal))
                    issues.Add(new("package.version", $"Project '{name}' package '{package}' must be exactly {expectedVersion}."));
            }
            foreach (var (package, _) in expectedPackages)
                if (!project.Packages.ContainsKey(package))
                    issues.Add(new("package.missing", $"Project '{name}' must reference package '{package}'."));
        }

        if (HasCycle(graph))
            issues.Add(new("project.cycle", "Production ProjectReference graph contains a cycle."));
        return issues;
    }

    private static bool HasCycle(IReadOnlyDictionary<string, List<string>> graph)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string node)
        {
            if (visited.Contains(node))
                return false;
            if (!visiting.Add(node))
                return true;
            foreach (var dependency in graph[node])
                if (Visit(dependency))
                    return true;
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }
        return graph.Keys.Any(Visit);
    }

    private static IReadOnlyDictionary<string, string> Packages(params (string Name, string Version)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => entry.Version, StringComparer.Ordinal);

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(NormalizeSeparators(path));

    private sealed record ParsedProject(
        string Path,
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyDictionary<string, string> Packages);
}

internal sealed record ProjectPolicyIssue(string Code, string Message);
