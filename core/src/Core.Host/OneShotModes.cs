using Core.Host.Domains;
using Core.Host.Loss;
using Core.Host.Operators;
using Core.Host.Rotation;

namespace Core.Host;

/// <summary>
/// The modes that run once and serve nothing, other than the migration: with the owner credential, the rotation of the epoch, the creation of a domain,
/// the rotation and the revocation of its label and the declaration of the loss of a key; with the application one, the first person.
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

        if (FirstOperatorMode.Requested(args))
        {
            return FirstOperatorMode.RunAsync(args, Console.In, Console.Out, Console.Error);
        }

        if (DomainGenerationMode.Requested(args))
        {
            return DomainGenerationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (DomainRevocationMode.Requested(args))
        {
            return DomainRevocationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (KeyLossMode.Requested(args))
        {
            return KeyLossMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (RecoveryRehearsalMode.Requested(args))
        {
            return RecoveryRehearsalMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (RecoveryRecordMode.Requested(args))
        {
            return RecoveryRecordMode.RunAsync(args, Console.Out, Console.Error);
        }

        return DomainCreationMode.Requested(args) ? DomainCreationMode.RunAsync(args, Console.Out, Console.Error) : null;
    }
}
