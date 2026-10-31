namespace Core.Persistence.People;

/// <summary>What the registry needs to add a person.</summary>
/// <param name="Name">The name of use, unique among people and without white space at either end.</param>
/// <param name="Function">One of <see cref="PersonFunctions.Accepted" />.</param>
public sealed record NewPerson(string Name, string Function);
