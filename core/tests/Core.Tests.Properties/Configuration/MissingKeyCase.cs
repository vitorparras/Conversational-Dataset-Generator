using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>Sources that were valid until a required key was taken out of every one of them, and that key's path.</summary>
internal sealed record MissingKeyCase(IReadOnlyList<IConfigurationSource> Sources, string Path);
