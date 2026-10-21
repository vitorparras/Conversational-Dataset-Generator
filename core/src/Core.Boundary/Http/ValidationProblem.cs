namespace Core.Boundary.Http;

/// <summary>Why an answer of the language service was refused whole; it names the problem, never the text or an offset.</summary>
public enum ValidationProblem
{
    /// <summary>The service declared a version of the contract the core does not know.</summary>
    UnknownContractVersion = 0,

    /// <summary>The body is not one well-formed JSON value in UTF-8: truncated, broken, or with a field repeated.</summary>
    MalformedJson = 1,

    /// <summary>The body has a field the contract does not declare, such as a confidence.</summary>
    UnknownField = 2,

    /// <summary>The body lacks a field the contract requires.</summary>
    MissingField = 3,

    /// <summary>A field holds a value of another type than the contract declares, or a null the contract does not allow.</summary>
    WrongValue = 4,

    /// <summary>An occurrence ends before it starts.</summary>
    EndBeforeStart = 5,

    /// <summary>An occurrence starts before the text or ends past it, counted in code points.</summary>
    OffsetOutsideText = 6,
}
