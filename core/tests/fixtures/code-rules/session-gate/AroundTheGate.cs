namespace Zqx.Fixtures;

internal static class AroundTheGate
{
    internal static void Compose(dynamic builder, dynamic path)
    {
        builder.MapGet("/x", () => 1).ShortCircuit();
        builder.MapShortCircuit(404, "/y");
        builder.MapWhen(context => true, branch => { });
        builder.UseWhen(context => true, branch => { });
        builder.Run(context => Task.CompletedTask);
        builder.Map("/branch", branch => { });
        builder.Map(new PathString("/branch"), branch => { });
        builder?.Map(path, branch => { });
        _ = new ShortCircuitMetadata(404);
    }
}
