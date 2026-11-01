using System.Text.RegularExpressions;
using Core.Persistence.Custody;

namespace Core.Persistence.Tests.Custody;

/// <summary>The names the chain writes come from lists in the code: every value of every list has one, written as a name and never as free text (requirement 021).</summary>
public sealed partial class CustodyNamesTests
{
    public static TheoryData<CustodyItemKind> Kinds => [.. Enum.GetValues<CustodyItemKind>()];

    public static TheoryData<CustodyStep> Steps => [.. Enum.GetValues<CustodyStep>()];

    public static TheoryData<CustodyReason> Reasons => [.. Enum.GetValues<CustodyReason>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindHasANameInSnakeCase(CustodyItemKind kind) =>
        Assert.Matches(SnakeCase(), CustodyNames.Of(kind));

    [Theory]
    [MemberData(nameof(Steps))]
    public void EveryStepHasANameInSnakeCase(CustodyStep step) =>
        Assert.Matches(SnakeCase(), CustodyNames.Of(step));

    [Theory]
    [MemberData(nameof(Reasons))]
    public void EveryReasonHasANameInSnakeCase(CustodyReason reason) =>
        Assert.Matches(SnakeCase(), CustodyNames.Of(reason));

    [Fact]
    public void NoTwoStepsShareAName()
    {
        var names = Enum.GetValues<CustodyStep>().Select(CustodyNames.Of);

        Assert.Equal(Enum.GetValues<CustodyStep>().Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void NoTwoReasonsShareAName()
    {
        var names = Enum.GetValues<CustodyReason>().Select(CustodyNames.Of);

        Assert.Equal(Enum.GetValues<CustodyReason>().Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void NamesAreTheOnesTheStoredEventsAreRecordedUnder()
    {
        Assert.Equal(
            [
                "person", "item_text", "reversal_map", "custody_chain",
                "person_created", "person_deactivated", "person_function_changed", "person_password_set", "correction",
                "raw_text_read", "sanitized_text_read", "reversal_entries_read", "verify_chain",
                "administrative_act", "correction_of_record", "content_read", "verification_passed", "verification_failed",
            ],
            [.. Enum.GetValues<CustodyItemKind>().Select(CustodyNames.Of), .. Enum.GetValues<CustodyStep>().Select(CustodyNames.Of), .. Enum.GetValues<CustodyReason>().Select(CustodyNames.Of)]);
    }

    [GeneratedRegex("^[a-z]+(_[a-z]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SnakeCase();
}
