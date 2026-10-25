namespace Core.Host.Tests.Privacy;

/// <summary>An item as a careless module might log it: its text carries no classification, so nothing erases it.</summary>
internal sealed record UnclassifiedItem(string Text);
