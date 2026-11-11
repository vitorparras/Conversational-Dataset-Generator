namespace Core.Host;

/// <summary>How the connection to the server of e-mail is ciphered: from the first byte, or raised before anything is said; never in clear.</summary>
internal enum MailTransport
{
    /// <summary>Ciphered from the first byte.</summary>
    Tls,

    /// <summary>Raised to a ciphered channel before the sender, the address or the message are said.</summary>
    StartTls,
}
