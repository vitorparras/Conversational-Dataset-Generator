namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>The password and the four names of the run, made up before the host starts so that a case can name them in what it registers.</summary>
internal sealed class ScreenPeople
{
    /// <summary>The password every person of the run has.</summary>
    public string Password { get; } = $"ZQX-SENHA-{Guid.NewGuid():N}";

    /// <summary>The person of the technical administration.</summary>
    public string Administrator { get; } = $"zqx-administracao-{Guid.NewGuid():N}";

    /// <summary>The person of the audit.</summary>
    public string Auditor { get; } = $"zqx-auditoria-{Guid.NewGuid():N}";

    /// <summary>The person of the curation of the collection, with no domain granted.</summary>
    public string Curator { get; } = $"zqx-curadoria-{Guid.NewGuid():N}";

    /// <summary>The person of an annotation, with no domain granted.</summary>
    public string Annotator { get; } = $"zqx-anotacao-{Guid.NewGuid():N}";
}
