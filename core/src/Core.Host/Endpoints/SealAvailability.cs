namespace Core.Host.Endpoints;

/// <summary>Whether the seal can be issued and recorded by hand: the installation declared Manual and has a chain to read.</summary>
internal enum SealAvailability
{
    Open,
    NotManual,
    Unreadable,
}
