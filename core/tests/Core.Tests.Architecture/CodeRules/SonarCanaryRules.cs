namespace Core.Tests.Architecture.CodeRules;

/// <summary>The Sonar finding planted in every type of every production file must come out of the build as an error, which no skipped file or type can hide.</summary>
internal static class SonarCanaryRules
{
    private const string Error = "error " + SonarCanary.Rule;

    public static IEnumerable<Violation> CheckRefused(IEnumerable<CanarySite> sites, CommandOutcome build) =>
        sites.SelectMany(site => site.RouteLines.Select(line => (Site: site, Line: line)))
            .Where(planted => !IsRefused(build, planted.Site, planted.Line))
            .Select(planted => new Violation(planted.Site.Source, 1, $"{SonarCanary.Rule} planted for this file was not reported (canary line {planted.Line})"));

    private static bool IsRefused(CommandOutcome build, CanarySite site, int line) =>
        build.Lines.Any(output =>
            output.Contains($"{site.CanaryPath}({line},", StringComparison.Ordinal) && output.Contains(Error, StringComparison.Ordinal));
}
