using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>Valid sources plus one more that writes a key no section declares, and that key as written.</summary>
internal sealed record UnknownKeyCase(IReadOnlyList<IConfigurationSource> Sources, string Key);
