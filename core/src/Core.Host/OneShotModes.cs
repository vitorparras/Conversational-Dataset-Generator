using Core.Host.Domains;
using Core.Host.Loss;
using Core.Host.Operators;
using Core.Host.Restore;
using Core.Host.Rotation;
using Core.Host.Settings;

namespace Core.Host;

/// <summary>
/// The modes that run once and serve nothing, other than the migration, in two groups by the credential each one uses. With the owner credential: the rotation of the epoch, the creation of a
/// domain, the rotation, the revocation and the rewrap of its label, the declaration of the loss of a key, the record of the recovery test and the copy of the data. With the application one: the
/// first person, the rehearsal of a restore and the rehearsal of the recovery; and the applier of the configuration, with a credential of its own that only reads.
/// </summary>
internal static class OneShotModes
{
    /// <summary>The run of the mode the first argument asks for, the owner's group first; null when it asks for none of them.</summary>
    internal static Task<HostLaunch>? RunAsync(string[] args) => OwnerModeAsync(args) ?? ApplicationModeAsync(args);

    private static Task<HostLaunch>? OwnerModeAsync(string[] args)
    {
        if (EpochRotationMode.Requested(args))
        {
            return EpochRotationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (DomainGenerationMode.Requested(args))
        {
            return DomainGenerationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (DomainRevocationMode.Requested(args))
        {
            return DomainRevocationMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (DomainRewrapMode.Requested(args))
        {
            return DomainRewrapMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (KeyLossMode.Requested(args))
        {
            return KeyLossMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (RecoveryRecordMode.Requested(args))
        {
            return RecoveryRecordMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (CopyDataMode.Requested(args))
        {
            return CopyDataMode.RunAsync(args, Console.Out, Console.Error);
        }

        return DomainCreationMode.Requested(args) ? DomainCreationMode.RunAsync(args, Console.Out, Console.Error) : null;
    }

    private static Task<HostLaunch>? ApplicationModeAsync(string[] args)
    {
        if (FirstOperatorMode.Requested(args))
        {
            return FirstOperatorMode.RunAsync(args, Console.In, Console.Out, Console.Error);
        }

        if (RecoveryRehearsalMode.Requested(args))
        {
            return RecoveryRehearsalMode.RunAsync(args, Console.Out, Console.Error);
        }

        if (ApplyConfigurationMode.Requested(args))
        {
            return ApplyConfigurationMode.RunAsync(args, Console.Out, Console.Error);
        }

        return RehearsalMode.Requested(args) ? RehearsalMode.RunAsync(args, Console.Out, Console.Error) : null;
    }
}
