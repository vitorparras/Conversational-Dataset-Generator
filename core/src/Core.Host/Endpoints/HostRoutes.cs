namespace Core.Host.Endpoints;

/// <summary>Every route the host serves, behind what it asks of the browser on the encrypted channel.</summary>
internal static class HostRoutes
{
    internal static void Map(WebApplication app, RecognizerKind recognizer)
    {
        TransportSecurity.Map(app);
        StatusEndpoint.Map(app, recognizer);
        ConfigurationEndpoint.Map(app);
        VersionsEndpoint.Map(app);
        AnalysisEndpoint.Map(app);
    }
}
