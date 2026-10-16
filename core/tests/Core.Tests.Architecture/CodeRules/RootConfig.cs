namespace Core.Tests.Architecture.CodeRules;

/// <summary>The config at the root of the tree, where a rule may be switched off with a reason: its text and the folder its sections are relative to.</summary>
internal sealed record RootConfig(string Text, string Directory);
