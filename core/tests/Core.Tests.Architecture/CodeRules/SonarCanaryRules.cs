namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The Sonar findings planted in every type of every production file must come out of the build as errors, which no skipped file or type can hide,
/// whether the rule is on by the root config or by default.
/// </summary>
internal static class SonarCanaryRules
{
    public static IEnumerable<Violation> CheckRefused(IEnumerable<CanarySite> sites, CommandOutcome build) =>
        sites.SelectMany(Planted)
            .Where(planted => !IsRefused(build, planted.Site, planted.Line, planted.Rule))
            .Select(planted => new Violation(planted.Site.Source, 1, $"{planted.Rule} planted for this file was not reported (canary line {planted.Line})"));

    private static IEnumerable<(CanarySite Site, int Line, string Rule)> Planted(CanarySite site) =>
        site.RouteLines.Select(line => (site, line, SonarCanary.Rule))
            .Concat((site.DefaultOnLines ?? []).Select(line => (site, line, SonarCanary.DefaultOnRule)));

    private static bool IsRefused(CommandOutcome build, CanarySite site, int line, string rule) =>
        build.Lines.Any(output =>
            output.Contains($"{site.CanaryPath}({line},", StringComparison.Ordinal) && output.Contains("error " + rule, StringComparison.Ordinal));
}
