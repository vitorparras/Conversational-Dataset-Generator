using System.Runtime.CompilerServices;

namespace Core.Host.Tests;

/// <summary>
/// Raises the floor of the thread pool for the whole test process. The suite runs a dozen classes at once and many of them
/// wait for a process or a server with a blocking call, which holds a pool thread each; with the default floor (the core
/// count) the pool adds one thread every half second, so the continuation that reads the port a service announced, or
/// answers its health, waited seconds behind them. Measured on the full solution, the wait went from up to 14 s to under
/// 40 ms, and the start of the real service from up to 65 s to under 10 s. Threads are still made only on demand.
/// </summary>
internal static class ThreadPoolFloor
{
    internal const int Threads = 256;

    [ModuleInitializer]
    internal static void Raise() => ThreadPool.SetMinThreads(Threads, Threads);
}
