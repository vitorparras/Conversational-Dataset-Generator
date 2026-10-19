namespace SonarRules;

/// <summary>A private method nothing calls, the typical finding of rule S1144, which the default profile already reports.</summary>
internal static class UnusedPrivateMember
{
    internal static int Next(int value) => value + 1;

    private static int Forgotten(int value) => value - 1;
}
