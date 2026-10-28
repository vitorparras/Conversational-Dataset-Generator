namespace Core.Host.Tests.Supervision;

/// <summary>Asks a question again and again until it holds or the limit passes.</summary>
internal static class Poll
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    /// <summary>How long the end of a killed service process may take to be seen; the wait returns as soon as it is seen, so a loaded machine costs time only when it needs it.</summary>
    public static readonly TimeSpan LossLimit = TimeSpan.FromSeconds(60);

    /// <summary>Whether the condition came to hold within the limit.</summary>
    public static async Task<bool> UntilAsync(Func<Task<bool>> condition, TimeSpan limit, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + limit;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(Interval, cancellationToken);
        }

        return await condition();
    }
}
