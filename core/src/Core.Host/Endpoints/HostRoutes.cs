namespace Core.Host.Endpoints;

/// <summary>Every route the host serves, behind what it asks of the browser on the encrypted channel and, for the routes that require one, behind a session.</summary>
internal static class HostRoutes
{
    internal static void Map(WebApplication app, RecognizerKind recognizer)
    {
        TransportSecurity.Map(app);
        SessionGate.Use(app);
        StatusEndpoint.Map(app, recognizer);
        ConfigurationEndpoint.Map(app);
        VersionsEndpoint.Map(app);
        AnalysisEndpoint.Map(app);
        SessionEndpoint.Map(app);
    }
}
