namespace Fixtures;

internal interface IWide
{
    int Combine(int a, int b, int c, int d, int e);
}

internal sealed class Wide : IWide
{
    int IWide.Combine(int a, int b, int c, int d, int e) => a + b + c + d + e;
}
