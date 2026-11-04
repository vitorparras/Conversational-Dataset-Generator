using Core.Host.Domains;
using Core.Host.Operators;
using Core.Host.Rotation;

namespace Core.Host;

/// <summary>The one-shot modes besides the migration: the epoch rotation, the domain creation with the owner credential and the first person with the application one.</summary>
internal static class OneShotModes
{
    /// <summary>The run of the mode the first argument asks for; null when it asks for none of them.</summary>
    internal static Task<HostLaunch>? RunAsync(string[] args)
    {
        if (EpochRotationMode.Requested(args))
        {
            return EpochRotationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (FirstOperatorMode.Requested(args))
        {
            return FirstOperatorMode.RunAsync(args, Console.In, Console.Out, Console.Error);
        }

        return DomainCreationMode.Requested(args) ? DomainCreationMode.RunAsync(args, Console.Out, Console.Error) : null;
    }
}
