namespace SonarRules;

/// <summary>A <c>break</c> in a loop, the finding of rule S1227, which the default profile leaves off.</summary>
internal static class BreakOutsideSwitch
{
    internal static int CountBeforeFirstNegative(int[] values)
    {
        var count = 0;
        foreach (var value in values)
        {
            if (value < 0)
            {
                break;
            }

            count++;
        }

        return count;
    }
}
