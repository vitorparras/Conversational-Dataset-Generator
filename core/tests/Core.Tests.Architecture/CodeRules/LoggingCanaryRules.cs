namespace Core.Tests.Architecture.CodeRules;

/// <summary>Every banned logging route, planted in every production file, must come out of the build as an error of the banned-symbol diagnostic.</summary>
internal static class LoggingCanaryRules
{
    private const string Error = "error " + LoggingSuppressionRules.Diagnostic;
    private static readonly string[] InfrastructureErrors = ["error MSB", "error NU", "error CS"];

    public static IEnumerable<Violation> CheckRoutesRefused(IReadOnlyList<CanarySite> sites, BuildOutcome build) =>
        sites.SelectMany(site => site.RouteLines.Select((line, route) => (Site: site, Line: line, Route: LoggingCanary.Routes[route])))
            .Where(planted => !IsRefused(build, planted.Site, planted.Line))
            .Select(planted => new Violation(planted.Site.RelativePath, planted.Line, $"{planted.Route} was not refused with {LoggingSuppressionRules.Diagnostic}"));

    /// <summary>Build lines that say the project did not build for a reason other than the banned symbol, so that a broken build never passes as a refusal.</summary>
    public static IEnumerable<string> CheckBuildIsIntact(BuildOutcome build) =>
        build.Lines.Where(line => InfrastructureErrors.Any(error => line.Contains(error, StringComparison.Ordinal)));

    private static bool IsRefused(BuildOutcome build, CanarySite site, int line) =>
        build.Lines.Any(output =>
            output.Contains($"{site.RelativePath}({line},", StringComparison.Ordinal) && output.Contains(Error, StringComparison.Ordinal));
}
