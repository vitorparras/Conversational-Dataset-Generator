using Core.Host;

var launch = await HostEntry.BuildAsync(args);
return (int)await launch.RunAsync();
