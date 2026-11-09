using Core.Host.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The size of the body of the two routes that receive a seal (issue #62, part B2b): a body that announces more than the limit
/// is 413 before it is read, one that announces nothing is limited as it is read, and other routes are left alone.
/// </summary>
public sealed class SealBodyLimitTests
{
    [Fact]
    public async Task ABodyThatAnnouncesMoreThanTheLimitIs413AndNeverReachesTheRoute()
    {
        var (context, reached) = await RunAsync("POST", "/custody/seal/confirmation", SealBodyLimit.Bytes + 1);

        Assert.Equal((StatusCodes.Status413PayloadTooLarge, false), (context.Response.StatusCode, reached));
    }

    [Theory]
    [InlineData("/custody/verification")]
    [InlineData("/custody/seal/confirmation")]
    [InlineData("/custodia/selo/confirmar")]
    [InlineData("/Custodia/Selo/Confirmar")]
    [InlineData("/Custody/Seal/Confirmation")]
    public async Task ABodyThatAnnouncesNothingIsLimitedAsItIsReadAndTheRouteIsReached(string route)
    {
        var (context, reached) = await RunAsync("POST", route, null);

        Assert.Equal((SealBodyLimit.Bytes, true), (context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize, reached));
    }

    [Fact]
    public async Task AnotherRouteOrAnotherMethodIsLeftAlone()
    {
        var (other, reachedOther) = await RunAsync("POST", "/analysis", SealBodyLimit.Bytes + 1);
        var (read, reachedRead) = await RunAsync("GET", "/custody/verification", SealBodyLimit.Bytes + 1);

        Assert.Equal(
            (null, true, null, true),
            (
                other.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize,
                reachedOther,
                read.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize,
                reachedRead));
    }

    private static async Task<(HttpContext Context, bool Reached)> RunAsync(string method, string path, long? length)
    {
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        var reached = false;
        SealBodyLimit.Use(builder);
        builder.Run(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new Limit());
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentLength = length;

        await builder.Build()(context);

        return (context, reached);
    }

    private sealed class Limit : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; }
    }
}
