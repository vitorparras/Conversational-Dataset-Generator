using System.Net;
using Core.Host;
using Core.Host.Endpoints;
using Core.Platform.Configuration;
using Core.Platform.Startup;

var schema = new ConfigurationSchema([HostConfiguration.Server]);
var load = ClosedConfiguration.Load(ConfigurationSources.Standard(AppContext.BaseDirectory, args), schema);
if (load.Configuration is not { } configuration)
{
    await Console.Error.WriteLineAsync("Configuração recusada; o núcleo não inicia:");
    foreach (var problem in load.Problems)
    {
        await Console.Error.WriteLineAsync("  " + problem.Describe());
    }

    return ExitCodes.ConfigurationRefused;
}

var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseKestrelCore();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(
    IPAddress.Parse(configuration.Get(HostConfiguration.Address)),
    int.Parse(configuration.Get(HostConfiguration.Port), System.Globalization.CultureInfo.InvariantCulture)));
builder.Services.AddRoutingCore();

var app = builder.Build();
StatusEndpoint.Map(app);
ConfigurationEndpoint.Map(app, configuration);

await app.RunAsync();
return ExitCodes.Requested;
