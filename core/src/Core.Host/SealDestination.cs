using System.Net;
using System.Net.NetworkInformation;

namespace Core.Host;

/// <summary>
/// What the configuration lets the host recognise of a destination under the same control as the database, which the host cannot see from here: an address that is not https, a file path,
/// the machine itself, loopback and the server of the database. Any other destination is the installation's word, and the verification says so.
/// </summary>
internal static class SealDestination
{
    private const int DriveLetterAndColon = 2;

    /// <summary>The rule the destination breaks, in words; null when it breaks none that can be recognised.</summary>
    /// <param name="destination">The destination as declared.</param>
    /// <param name="databaseHost">The server of the database as declared, with its port when it has one; null when none is declared.</param>
    internal static string? Refusal(string destination, string? databaseHost)
    {
        if (IsFilePath(destination))
        {
            return "é um caminho de arquivo, que está sob o controle da máquina e não é um destino";
        }

        if (!Uri.TryCreate(destination, UriKind.Absolute, out var address))
        {
            return "não é um endereço absoluto";
        }

        return address.Scheme != Uri.UriSchemeHttps ? "o esquema não é https" : RefusalOfHost(address, databaseHost);
    }

    private static string? RefusalOfHost(Uri address, string? databaseHost)
    {
        if (address.UserInfo.Length > 0)
        {
            return "leva usuário ou senha no endereço";
        }

        if (address.IsLoopback)
        {
            return "é loopback (localhost, 127.0.0.0/8 ou ::1), a própria máquina";
        }

        if (IsThisMachine(address.Host))
        {
            return "é o nome ou o endereço da própria máquina";
        }

        return databaseHost is not null && string.Equals(address.Host, HostOf(databaseHost), StringComparison.OrdinalIgnoreCase) ? "é o mesmo servidor do banco" : null;
    }

    private static bool IsFilePath(string destination) =>
        destination.StartsWith('/') || destination.StartsWith('\\') || IsDrivePath(destination) || destination.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    private static bool IsDrivePath(string destination) =>
        destination.Length > DriveLetterAndColon && char.IsAsciiLetter(destination[0]) && destination[1] == ':' && destination[DriveLetterAndColon] is '\\' or '/';

    private static string HostOf(string databaseHost)
    {
        var colon = databaseHost.LastIndexOf(':');
        return colon < 0 ? databaseHost : databaseHost[..colon];
    }

    private static bool IsThisMachine(string host)
    {
        var machine = new[] { Environment.MachineName, Dns.GetHostName() };
        if (Array.Exists(machine, name => host.Equals(name, StringComparison.OrdinalIgnoreCase) || host.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && (ip.Equals(IPAddress.Any) || IsOfAnInterface(ip));
    }

    private static bool IsOfAnInterface(IPAddress ip) =>
        NetworkInterface.GetAllNetworkInterfaces().SelectMany(network => network.GetIPProperties().UnicastAddresses).Any(unicast => unicast.Address.Equals(ip));
}
