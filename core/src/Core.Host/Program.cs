using Core.Host.Endpoints;

var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = args });
builder.WebHost.UseKestrelCore();
builder.Services.AddRoutingCore();

var app = builder.Build();
app.Urls.Add("http://127.0.0.1:5080");
StatusEndpoint.Map(app);

await app.RunAsync();
