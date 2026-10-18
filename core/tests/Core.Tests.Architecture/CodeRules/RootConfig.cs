namespace Core.Tests.Architecture.CodeRules;

/// <summary>The config at the root of the tree, where a rule may be switched off with a reason: its text, the folder its sections are relative to and the file itself.</summary>
internal sealed record RootConfig(string Text, string Directory, string Path);
