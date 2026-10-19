namespace Skips;

[System.AttributeUsage(System.AttributeTargets.Class)]
internal sealed class GeneratedCodeAttribute : System.Attribute
{
}

[GeneratedCode]
internal static class OwnAttr
{
    internal const int Value = 10;
}
