using System.Text.Json;
using ShipGame.Domain;

namespace ShipGame.Content;

public sealed class ContentValidationException(IReadOnlyList<ValidationIssue> issues)
    : Exception(string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message}")))
{
    public IReadOnlyList<ValidationIssue> Issues { get; } = issues;
}
