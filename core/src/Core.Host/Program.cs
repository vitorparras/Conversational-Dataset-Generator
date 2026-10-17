using Core.Host;

var launch = await CompositionRoot.BuildAsync(args);
return (int)launch.Run();
