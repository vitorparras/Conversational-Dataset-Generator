namespace Core.Host.Tests;

/// <summary>A database address where nothing listens: port 1 refuses the connection before any credential is asked, so a read of the chain fails at once and is told in the log.</summary>
internal static class UnreachableDatabase
{
    internal const string ConnectionString = "Host=127.0.0.1;Port=1;Username=zqx;Database=zqx;Timeout=2;Pooling=false";
}
