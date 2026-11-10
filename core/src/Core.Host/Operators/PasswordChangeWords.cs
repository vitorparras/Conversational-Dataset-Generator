namespace Core.Host.Operators;

/// <summary>The words of the refusals of the change of the own password, written once: the screen and the route of JSON say the same thing in the same words.</summary>
internal static class PasswordChangeWords
{
    /// <summary>The current password is not the one the person has, or the try had to wait: one text, so that it never tells which.</summary>
    internal const string WrongCurrent = "A senha atual não confere. Confira e tente de novo.";

    /// <summary>The new password is empty.</summary>
    internal const string BlankReplacement = "Informe a nova senha.";

    /// <summary>The two new passwords typed are not the same, so none is written.</summary>
    internal const string Mismatch = "As duas senhas novas não são iguais. Digite de novo.";
}
