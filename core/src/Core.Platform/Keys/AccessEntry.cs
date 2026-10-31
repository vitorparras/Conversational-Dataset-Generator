namespace Core.Platform.Keys;

/// <summary>One entry of the access list of a file: who, by SID and by name, whether it allows or denies, and the rights as the bits the system gave them.</summary>
internal readonly record struct AccessEntry(string Sid, string Account, bool Allows, int Rights);
