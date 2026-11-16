using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>What the host has made when a case registers its services: the database of the run, migrated, and the people it will hold.</summary>
internal sealed record ScreenSetup(ScratchDatabase Database, ScreenPeople People);
