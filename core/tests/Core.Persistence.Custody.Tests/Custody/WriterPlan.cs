using CsCheck;

namespace Core.Persistence.Tests.Custody;

/// <summary>Who signs one planned append: the system, a person who is active, a person who was deactivated, or one nobody registered.</summary>
internal enum PlannedAuthor
{
    System,
    Active,
    Deactivated,
    Unknown,
}

/// <summary>One append a writer asks for, and whether it corrects the event the case began with.</summary>
internal sealed record PlannedAppend(PlannedAuthor Author, bool Corrects);

/// <summary>Several writers, each asking for its appends one after the other, all of them at the same time.</summary>
internal sealed record WriterPlan(IReadOnlyList<IReadOnlyList<PlannedAppend>> Writers)
{
    internal const int MostAppendsPerWriter = 4;

    internal static Gen<WriterPlan> Generator { get; } = Gen
        .Select(Gen.Int[0, 5].Select(Author), Gen.Bool, (author, corrects) => new PlannedAppend(author, corrects))
        .List[1, MostAppendsPerWriter - 1]
        .List[2, 5]
        .Select(writers => new WriterPlan([.. writers.Select(appends => (IReadOnlyList<PlannedAppend>)appends)]));

    private static PlannedAuthor Author(int draw) => draw switch
    {
        0 or 1 => PlannedAuthor.System,
        2 or 3 => PlannedAuthor.Active,
        4 => PlannedAuthor.Deactivated,
        _ => PlannedAuthor.Unknown,
    };
}
