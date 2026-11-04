using System.Net;
using System.Security.Authentication;
using Core.Boundary.Http;

namespace Core.Boundary.Tests;

/// <summary>
/// The sending of the seal to the destination (issue #62, part B2a): only an answer of success is a delivery, a certificate that is not accepted, a refusal of the destination, a time that runs out
/// and a redirect are failures that say why and never carry the seal, and the handler the host sends with follows no redirect and has no way round the validation of the certificate.
/// </summary>
public sealed class SealDeliveryTests
{
    private const string Text = "CDG-SELO-1|2031-03-04T05:06:07Z|5|0000000000000000000000000000000000000000000000000000000000000000|0000";
    private static readonly Uri Destination = new("https://selo.example/envio");

    [Fact]
    public async Task AnswerOfSuccessIsADeliveryAndTheTextIsPostedAsItIs()
    {
        using var handler = new Stub(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((true, SealDeliveryFailure.None, HttpMethod.Post, Destination, Text), (result.Delivered, result.Failure, handler.Method, handler.Target, handler.Body));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    public async Task AnswerThatIsNotASuccessIsAFailureThatSaysWhichAndDoesNotFollowTheRedirect(HttpStatusCode status)
    {
        using var handler = new Stub(() => Redirecting(status));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((SealDeliveryFailure.DestinationAnswered, (int)status, 1), (result.Failure, result.Status, handler.Calls));
    }

    [Fact]
    public async Task CertificateThatIsNotAcceptedIsAFailureThatSaysSoAndDoesNotCarryTheSeal()
    {
        using var handler = new Stub(() => throw new HttpRequestException("zqx", new AuthenticationException("zqx")));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((false, SealDeliveryFailure.CertificateRejected), (result.Delivered, result.Failure));
    }

    [Fact]
    public async Task DestinationThatIsNotReachedIsAFailureThatSaysSo()
    {
        using var handler = new Stub(() => throw new HttpRequestException("zqx"));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((false, SealDeliveryFailure.Unreachable), (result.Delivered, result.Failure));
    }

    [Fact]
    public async Task TimeThatRunsOutIsAFailureThatSaysSo()
    {
        using var handler = new Stub(() => throw new TaskCanceledException("zqx"));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((false, SealDeliveryFailure.TimedOut), (result.Delivered, result.Failure));
    }

    [Fact]
    public async Task ConnectionThatTimesOutBeforeTheWholeRequestDoesIsTimeAndNotUnreachable()
    {
        using var handler = new Stub(() => throw new HttpRequestException("zqx", new TimeoutException("zqx")));
        using var delivery = new SealDelivery(Destination, handler);

        var result = await delivery.SendAsync(Text, TestContext.Current.CancellationToken);

        Assert.Equal((false, SealDeliveryFailure.TimedOut), (result.Delivered, result.Failure));
    }

    [Fact]
    public async Task CancellationByTheCallerIsNotAFailureOfTheDestination()
    {
        using var handler = new Stub(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var delivery = new SealDelivery(Destination, handler);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delivery.SendAsync(Text, cancelled.Token));

        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("http://selo.example/envio")]
    [InlineData("ftp://selo.example/envio")]
    public void AddressThatIsNotHttpsIsRefused(string address)
    {
        using var handler = new Stub(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        _ = Assert.Throws<ArgumentException>(() => new SealDelivery(new Uri(address), handler));
    }

    [Theory]
    [InlineData("http://selo.example/envio")]
    [InlineData("ftp://selo.example/envio")]
    public void DeliveryTheHostBuildsRefusesAnAddressThatIsNotHttps(string address)
    {
        _ = Assert.Throws<ArgumentException>(() => SealDelivery.To(new Uri(address)));
    }

    [Fact]
    public void DeliveryTheHostBuildsForAnHttpsAddressIsBuiltAndDisposed()
    {
        using var delivery = SealDelivery.To(Destination);

        Assert.NotNull(delivery);
    }

    [Fact]
    public void HandlerTheHostSendsWithFollowsNoRedirectLimitsTheConnectionAndLeavesTheValidationOfTheCertificateAsItIs()
    {
        using var handler = SealDelivery.Handler();

        Assert.Equal((false, TimeSpan.FromSeconds(10), true), (handler.AllowAutoRedirect, handler.ConnectTimeout, handler.SslOptions.RemoteCertificateValidationCallback is null));
    }

    private static HttpResponseMessage Redirecting(HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri("https://outro.example/");
        return response;
    }

    private sealed class Stub(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? Target { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Target = request.RequestUri;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return answer();
        }
    }
}
