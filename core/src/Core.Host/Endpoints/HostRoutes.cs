using Core.Host.Screens;

namespace Core.Host.Endpoints;

/// <summary>Every route the host serves, behind what it asks of the browser on the encrypted channel and behind a session, except the entry and the status.</summary>
internal static class HostRoutes
{
    internal static void Map(WebApplication app, RecognizerKind recognizer)
    {
        TransportSecurity.Map(app);
        ScreenFailure.Use(app);
        SessionGate.Use(app);
        ScreenGate.Use(app);
        SealBodyLimit.Use(app);
        StatusEndpoint.Map(app, recognizer);
        ConfigurationEndpoint.Map(app);
        VersionsEndpoint.Map(app);
        AnalysisEndpoint.Map(app);
        SessionEndpoint.Map(app);
        EntryPageEndpoint.Map(app);
        SystemScreensEndpoint.Map(app, recognizer);
        DomainScreensEndpoint.Map(app);
        AuditEndpoint.Map(app);
        OperatorsEndpoint.Map(app);
        AccountEndpoint.Map(app);
        SubmissionsEndpoint.Map(app);
        ChainVerificationEndpoint.Map(app);
        SealVerificationEndpoint.Map(app);
        SealEndpoint.Map(app);
        ScreenAssets.Map(app);
    }
}
