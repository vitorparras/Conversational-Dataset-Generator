namespace Core.Host.People;

/// <summary>What <c>create-person</c> was asked to make: the name, the function and the password from the standard input. It has no text form, so the password is never printed.</summary>
internal sealed class FirstPerson
{
    internal FirstPerson(string name, string function, string password)
    {
        Name = name;
        Function = function;
        Password = password;
    }

    internal string Name { get; }

    internal string Function { get; }

    internal string Password { get; }

    /// <summary>The identifier the person took once it was made; null until then.</summary>
    internal long? Id { get; set; }

    public override string ToString() => nameof(FirstPerson);
}
