namespace Core.Boundary.Http;

/// <summary>Why a seal did not arrive; <see cref="None" /> when it did.</summary>
public enum SealDeliveryFailure
{
    /// <summary>The seal arrived.</summary>
    None,

    /// <summary>The destination answered with something that is not a success; a redirect is one, since it is not followed.</summary>
    DestinationAnswered,

    /// <summary>The certificate of the destination was not accepted by the default validation.</summary>
    CertificateRejected,

    /// <summary>The destination was not reached.</summary>
    Unreachable,

    /// <summary>The destination did not answer, or did not accept the connection, within the time limit.</summary>
    TimedOut,
}
