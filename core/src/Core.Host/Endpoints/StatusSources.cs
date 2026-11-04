using Core.Host.Supervision;
using Core.Platform;
using Core.Platform.Startup;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>What the status route reads, each from the services of the host.</summary>
internal sealed record StatusSources(
    [FromServices] StartupReport Report,
    [FromServices] ServiceProcessWatch Watch,
    [FromServices] Installation Installation,
    [FromServices] AuditComparisonState Comparison,
    [FromServices] SealWarningState SealWarning);
