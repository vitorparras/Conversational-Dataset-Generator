namespace Core.Persistence.People;

/// <summary>Why an attempt to sign in was refused, as the chain records it.</summary>
public enum SignInRefusal
{
    /// <summary>The name is of a person who enters and the password was wrong.</summary>
    PasswordWrong,

    /// <summary>A name nobody goes by, or an account that does not enter: the name typed is not kept.</summary>
    AccountNotRecognized,
}
