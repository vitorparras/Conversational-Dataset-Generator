using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// What a case puts into the <see cref="ScreenHost"/>, which is sealed and takes it by composition: the services the screens read besides the session, the people and the data source, the
/// routes of the screens the test reaches, and what the case writes in the database. A case that wants another way of reaching the host gives its own <see cref="Pipeline"/>.
/// </summary>
internal sealed record ScreenParts
{
    /// <summary>The services the screens read, besides the session, the people and the data source; the setup holds the database of the run and the names of its people.</summary>
    public Action<IServiceCollection, ScreenSetup> Services { get; init; } = static (_, _) => { };

    /// <summary>The routes of the screens the test reaches, behind the standard pipeline.</summary>
    public Action<IEndpointRouteBuilder> Screens { get; init; } = static _ => { };

    /// <summary>The whole pipeline of the host, when the standard one does not serve the case; it maps its own routes.</summary>
    public Action<WebApplication, ScreenParts>? Pipeline { get; init; }

    /// <summary>What is written in the database once it is migrated and before the host starts.</summary>
    public Func<ScratchDatabase, Task> Prepare { get; init; } = static _ => Task.CompletedTask;

    /// <summary>What only the people of the host can write, such as a verification of the chain, once the host is up and they exist.</summary>
    public Func<ScreenHost, Task> Finish { get; init; } = static _ => Task.CompletedTask;

    /// <summary>What the case lets go of with the host, such as a post it keeps on the server.</summary>
    public Action Disposing { get; init; } = static () => { };

    /// <summary>The configuration in force of the host, when the case shows one; by default the one of the host with no key set.</summary>
    public EffectiveConfiguration? Configuration { get; init; }

    /// <summary>Whether the host makes the four people of the run; a case that makes its own says false.</summary>
    public bool WithPeople { get; init; } = true;
}
