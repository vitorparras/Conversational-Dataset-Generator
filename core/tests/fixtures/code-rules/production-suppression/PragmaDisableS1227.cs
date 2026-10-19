namespace Fixtures;

internal static class PragmaDisableS1227
{
    public static int CountBeforeFirstNegative(int[] values)
    {
        var count = 0;
        foreach (var value in values)
        {
#pragma warning disable S1227
            if (value < 0)
            {
                break;
            }
#pragma warning restore S1227

            count++;
        }

        return count;
    }
}
