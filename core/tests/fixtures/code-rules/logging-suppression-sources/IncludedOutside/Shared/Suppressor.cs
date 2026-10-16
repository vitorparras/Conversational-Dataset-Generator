using System.Diagnostics.CodeAnalysis;

namespace Shared;

[SuppressMessage("ApiDesign", "RS0030", Justification = "Fixture.")]
internal static class Suppressor
{
    public const int Value = 2;
}
