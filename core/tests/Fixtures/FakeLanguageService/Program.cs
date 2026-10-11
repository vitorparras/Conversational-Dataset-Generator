// Usage: FakeLanguageService [--mode serve|hang|exit] --port N, with the secret as one line on standard input.
//   serve: answers /health when the header carries the secret, and 401 otherwise (the default).
//   hang:  reads the secret and never listens, to exercise the startup timeout.
//   exit:  reads the secret and exits with code 7, to exercise a service that dies on start.
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

var mode = ValueOf("--mode") ?? "serve";
var port = int.Parse(ValueOf("--port") ?? throw new ArgumentException("--port is required"), CultureInfo.InvariantCulture);
var secret = await Console.In.ReadLineAsync() ?? throw new InvalidOperationException("no secret on standard input");

if (mode == "exit")
{
    return 7;
}

if (mode == "hang")
{
    await Task.Delay(Timeout.Infinite);
}

var expected = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
builder.WebHost.UseKestrelCore();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, port));
builder.Services.AddRoutingCore();
await using var app = builder.Build();
app.MapGet("/health", (HttpContext context) =>
{
    var given = context.Request.Headers["X-Service-Secret"].ToString();
    var matches = CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(Encoding.UTF8.GetBytes(given)));
    return matches
        ? Results.Json(new { status = "ok", model_version = (string?)null })
        : Results.Json(new { error = "unauthorized" }, statusCode: 401);
});
await app.RunAsync();
return 0;

static string? ValueOf(string name)
{
    var arguments = Environment.GetCommandLineArgs();
    var position = Array.IndexOf(arguments, name);
    return position >= 0 && position + 1 < arguments.Length ? arguments[position + 1] : null;
}
