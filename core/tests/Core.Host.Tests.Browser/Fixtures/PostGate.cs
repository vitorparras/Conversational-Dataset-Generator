using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// Tells the channel to be encrypted and, for a post of the requests it counts, counts it on the server and keeps it there while the test asks, so that a test can look at what the browser does
/// while the answer has not come.
/// </summary>
internal sealed class PostGate(Func<HttpRequest, bool> counts)
{
    private readonly Lock gate = new();
    private TaskCompletionSource? held;
    private int posts;

    /// <summary>How many of the posts it counts reached the server.</summary>
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

    /// <summary>From now on every post it counts waits on the server until <see cref="Release"/>.</summary>
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

    /// <summary>Waits up to ten seconds for the server to have counted the posts expected, and fails with how many it counted when it has not.</summary>
    public async Task WaitForAsync(int expected, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(10);
        while (Posts < expected && DateTimeOffset.UtcNow < until)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        Assert.Equal(expected, Posts);
    }

    public async Task ServeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Scheme = "https";
        if (HttpMethods.IsPost(context.Request.Method) && counts(context.Request))
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
