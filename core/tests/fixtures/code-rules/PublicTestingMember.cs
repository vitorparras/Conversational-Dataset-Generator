namespace Fixtures;

internal sealed class Counter
{
    private int count;

    public int Count => count;
    public void ResetForTest() => count = 0;

    public bool Testing { get; set; }

    public static int TestSeed = 7;
}
