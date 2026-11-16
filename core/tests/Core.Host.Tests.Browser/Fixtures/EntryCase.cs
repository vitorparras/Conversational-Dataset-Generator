using System.Diagnostics.CodeAnalysis;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the pages of the entry and of the exit, behind the real session gate and over a migrated database of its own: the real store of credentials, the real delay of a refusal and the
/// real session store. The registry holds nobody, or the one person of <see cref="Name"/> with the password of the host, both made up for the run. The channel is told to be encrypted, which the
/// loopback of the test cannot be; the entry itself is untouched. <see cref="HoldNextPost"/> keeps the post of the form on the server, so that a test can look at what the browser does while
/// the answer has not come.
/// </summary>
internal sealed class EntryCase : IAsyncDisposable
{
    private readonly PostGate gate;

    private EntryCase(ScreenHost host, string name, PostGate gate)
    {
        Host = host;
        Name = name;
        this.gate = gate;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>The address the server listens on.</summary>
    public Uri Address => Host.Address;

    /// <summary>The person of the run, who exists only when the case was started with one.</summary>
    public string Name { get; }

    /// <summary>The password of the person of the run.</summary>
    public string Password => Host.Password;

    /// <summary>How many posts of the entry reached the server.</summary>
    public int Posts => gate.Posts;

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<EntryCase> StartAsync(bool withPerson, CancellationToken cancellationToken)
    {
        var name = $"zqx-pessoa-{Guid.NewGuid():N}";
        var gate = new PostGate();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                WithPeople = false,
                Pipeline = (started, _) => Serve(started, gate),
                Finish = started => withPerson ? started.AddPersonAsync(name, PersonFunctions.Accepted[0], cancellationToken) : Task.CompletedTask,
                Disposing = gate.Release,
            },
            cancellationToken);
        return new EntryCase(host, name, gate);
    }

    private static void Serve(WebApplication started, PostGate gate)
    {
        _ = started.Use(gate.ServeAsync);
        SessionGate.Use(started);
        EntryPageEndpoint.Map(started);
        ScreenAssets.Map(started);
    }

    /// <summary>From now on the next post of the entry waits on the server until the test calls <see cref="Release"/>.</summary>
    public void HoldNextPost() => gate.Hold();

    public void Release() => gate.Release();

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    /// <summary>Tells the channel to be encrypted and, for a post of the entry, counts it and keeps it while the test asks.</summary>
    private sealed class PostGate
    {
        private readonly Lock gate = new();
        private TaskCompletionSource? held;
        private int posts;

        public int Posts
        {
            get
            {
                lock (gate)
                {
                    return posts;
                }
            }
        }

        public void Hold()
        {
            lock (gate)
            {
                held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public void Release()
        {
            lock (gate)
            {
                _ = held?.TrySetResult();
                held = null;
            }
        }

        public async Task ServeAsync(HttpContext context, RequestDelegate next)
        {
            context.Request.Scheme = "https";
            if (context.Request.Path.Equals(ScreenPaths.SignIn) && HttpMethods.IsPost(context.Request.Method))
            {
                Task? waiting;
                lock (gate)
                {
                    posts++;
                    waiting = held?.Task;
                }

                if (waiting is not null)
                {
                    await waiting.WaitAsync(context.RequestAborted);
                }
            }

            await next(context);
        }
    }
}
