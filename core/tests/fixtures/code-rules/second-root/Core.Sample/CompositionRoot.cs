namespace Core.Sample;

internal static class CompositionRoot
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<SampleClock>();
        return builder.Build();
    }
}

internal sealed class SampleClock;
