using System.Diagnostics.CodeAnalysis;

namespace Fixtures;

[SuppressMessage("ApiDesign", Ids.Banned, Justification = "The id is hidden behind a constant.")]
internal static partial class SampleLogMessages
{
}

internal static class Ids
{
    public const string Banned = "RS0030";
}
