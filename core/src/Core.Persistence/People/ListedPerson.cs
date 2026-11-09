namespace Core.Persistence.People;

/// <summary>A person as the list shows them: the name of use, the function declared and whether they are active. Nothing of any material, of any password or of any grant.</summary>
/// <param name="Id">The identifier the registry gave.</param>
/// <param name="Name">The name of use.</param>
/// <param name="Function">The function declared, one of the functions of the policy.</param>
/// <param name="Active">Whether the person is active; a person is deactivated and never deleted.</param>
public sealed record ListedPerson(long Id, string Name, string Function, bool Active);
