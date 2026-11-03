using Core.Host.Domains;
using Core.Host.KeyLoss;
using Core.Host.Rotation;

namespace Core.Host;

/// <summary>
/// The modes that run once with the owner credential and serve nothing, other than the migration: the rotation of the epoch, the creation of a domain,
/// the rotation of its label and the declaration of the loss of a key.
/// </summary>
internal static class OneShotModes
{
    /// <summary>The run of the mode the first argument asks for; null when it asks for none of them.</summary>
    internal static Task<HostLaunch>? RunAsync(string[] args)
    {
        if (EpochRotationMode.Requested(args))
        {
            return EpochRotationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (DomainGenerationMode.Requested(args))
        {
            return DomainGenerationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (KeyLossMode.Requested(args))
        {
            return KeyLossMode.RunAsync(args, Console.Out, Console.Error);
        }

        return DomainCreationMode.Requested(args) ? DomainCreationMode.RunAsync(args, Console.Out, Console.Error) : null;
    }
}
