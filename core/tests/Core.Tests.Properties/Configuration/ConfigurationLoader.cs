using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>The act of loading, as a property sees it: the production call, or a deliberately broken one.</summary>
internal delegate ConfigurationLoadResult ConfigurationLoader(
    ConfigurationSchema schema,
    IReadOnlyList<IConfigurationSource> sources);
