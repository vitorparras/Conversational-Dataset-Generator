using System.Diagnostics.CodeAnalysis;

namespace Fixtures;

[SuppressMessage("Major Code Smell", "S1144", Justification = "Kept for later.")]
internal static class SuppressMessageOnS1144
{
    private static int Forgotten(int value) => value - 1;
}
