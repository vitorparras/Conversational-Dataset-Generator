namespace Core.Host.Supervision;

/// <summary>Who asked for the language service to be restarted.</summary>
internal enum RestartRequester
{
    /// <summary>The person who operates the core.</summary>
    Operator = 0,
}
