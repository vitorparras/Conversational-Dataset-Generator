using Core.Platform.Logging;

namespace Core.Host.Tests.Logging;

/// <summary>An item as a module might log it through the host: one plain identifier and one material text.</summary>
internal sealed record SampleItem(string ItemId, [property: Material] string Text);
