namespace Fixtures;

internal static class Chain
{
    public static string Sign(int value)
    {
        if (value > 0)
        {
            return "positive";
        }
        else if (value < 0)
        {
            return "negative";
        }
        else if (value == 0)
        {
            return "zero";
        }

        return "unknown";
    }
}
