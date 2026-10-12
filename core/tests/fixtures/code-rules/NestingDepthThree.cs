namespace Fixtures;

internal static class Deep
{
    public static int Count(IEnumerable<int> values)
    {
        var count = 0;
        foreach (var value in values)
        {
            if (value > 0)
            {
                // The third level: the loop below should be its own method.
                while (count < value)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
