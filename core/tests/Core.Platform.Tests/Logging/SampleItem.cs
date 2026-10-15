using Core.Platform.Logging;

namespace Core.Platform.Tests.Logging;

/// <summary>An item as a module might log it: one plain identifier and one value of each sensitive classification.</summary>
internal sealed record SampleItem(
    string ItemId,
    [property: Material] string Text,
    [property: Identity] string Person,
    [property: Secret] string Token);
