using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>What the routes of the chain and of its seal read, each from the services of the host.</summary>
internal sealed record ChainSources(
    [FromServices] ChainVerificationService Verification,
    [FromServices] SealDeclaration Declared,
    [FromServices] SessionStore Sessions,
    [FromServices] TimeProvider Time,
    [FromServices] ILoggerFactory Loggers);
