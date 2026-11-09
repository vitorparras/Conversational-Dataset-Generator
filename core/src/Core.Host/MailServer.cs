namespace Core.Host;

/// <summary>The server of e-mail the installation declared; the password stays in the configuration and is read by the sending alone (#600), so it is not here.</summary>
/// <param name="Host">The name of the server.</param>
/// <param name="Port">Its port.</param>
/// <param name="Sender">The address the messages are sent from.</param>
/// <param name="Transport">How the connection is ciphered.</param>
/// <param name="User">The user the server takes, if it asks for one.</param>
internal sealed record MailServer(string Host, int Port, string Sender, MailTransport Transport, string? User);
