using System.Text;
using Core.Host.Endpoints;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Core.Host.Tests;

/// <summary>
/// The one reader of what a request carries (#817): the form, which is nothing when the body is not a form instead of a failure, and the session the cookie names, resolved
/// the same way for every route.
/// </summary>
public sealed class RequestReaderTests
{
    private const string FormType = "application/x-www-form-urlencoded";

    [Fact]
    public async Task AFormBodyIsReadWithItsFields()
    {
        var context = Posted(FormType, "zqx=um&zqy=dois");

        var form = await RequestReader.FormAsync(context);

        Assert.NotNull(form);
        Assert.Equal(["um", "dois"], [form["zqx"].ToString(), form["zqy"].ToString()]);
    }

    [Theory]
    [InlineData("application/json", "{}")]
    [InlineData("text/plain", "zqx=um")]
    public async Task ABodyThatIsNotAFormIsNothingAndNotAFailure(string contentType, string body)
    {
        var context = Posted(contentType, body);

        Assert.Null(await RequestReader.FormAsync(context));
    }

    [Fact]
    public async Task ARequestWithNoBodyAtAllIsNothing() => Assert.Null(await RequestReader.FormAsync(new DefaultHttpContext()));

    [Fact]
    public async Task TheReadStopsWhenTheRequestIsAborted()
    {
        using var aborted = new CancellationTokenSource();
        var context = Posted(FormType, "zqx=um");
        context.RequestAborted = aborted.Token;
        await aborted.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await RequestReader.FormAsync(context));
    }

    [Fact]
    public void TheSessionIsTheOneTheCookieNames()
    {
        var sessions = Store();
        var identifier = sessions.Open(7);

        Assert.Equal(7, RequestReader.SessionOf(WithCookie(sessions, identifier)));
    }

    [Fact]
    public void NoCookieAndAnIdentifierTheStoreDoesNotKnowAreNoSession()
    {
        var sessions = Store();

        Assert.Null(RequestReader.SessionOf(WithCookie(sessions, null)));
        Assert.Null(RequestReader.SessionOf(WithCookie(sessions, new string('0', 64))));
    }

    [Fact]
    public void AFormRequestIsToldFromOneThatIsNot()
    {
        Assert.True(RequestReader.CarriesForm(Posted(FormType, "zqx=um").Request));
        Assert.False(RequestReader.CarriesForm(Posted("application/json", "{}").Request));
    }

    private static SessionStore Store() => new(new SessionLifetimes(TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)), TimeProvider.System);

    private static DefaultHttpContext WithCookie(SessionStore sessions, string? identifier)
    {
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(sessions).BuildServiceProvider() };
        if (identifier is not null)
        {
            context.Request.Headers.Cookie = new StringValues($"{SessionCookie.Name}={identifier}");
        }

        return context;
    }

    private static DefaultHttpContext Posted(string contentType, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }
}
