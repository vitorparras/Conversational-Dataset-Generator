using System.Globalization;
using System.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using Xunit.Sdk;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// No text leaves the whole list of steps holding what the sanitization should have removed, in any recognizable variation, also when an invisible character splits it; sanitizing
/// twice gives what sanitizing once gave, and a text with nothing to remove leaves as it came (requirement 004, issue 94). The check is measured by a defect planted on purpose.
/// </summary>
public sealed class SanitizationResidueTests
{
    private const string Joiner = " e ";

    private static readonly Gen<Segment> Signal = SignalTexts.Of([.. SignalTexts.Kinds]).Select(sample => new Segment(sample.Text, null, null));

    private static readonly Gen<Segment> Kept = Gen.OneOf(
        Gen.Select(Gen.Long[0, 99_999_999_999], Gen.Int[0, 99], Gen.OneOf("R$ ", "US$ ", "€ "), Money),
        Gen.Select(Gen.Long[0, 9_999_999_999], Gen.OneOf(" kg", " ml", " litros", " m²", "%", " toneladas"), (amount, unit) => Plain(string.Create(CultureInfo.InvariantCulture, $"{amount}{unit}"))),
        Gen.Select(Gen.Int[1, 28], Gen.Int[1, 12], Gen.Int[1950, 2049], Gen.Int[0, 23], Gen.Int[0, 59], Gen.Int[0, 2], Date));

    private static readonly Gen<Segment> KeptNames = Gen.Select(Gen.OneOf("Wvkt", "Zqxw"), Gen.Bool, (name, pseudonymized) =>
        new Segment(name, (start, end) => pseudonymized ? Pseudonymized(start, end) : Preserved(start, end), null));

    private static readonly Gen<Segment> Names = Gen.Select(Gen.OneOf("Wvkt", "Zqxw", "Kq7Zx9"), Gen.Int[0, 3], (name, kind) => new Segment(name, (start, end) => kind switch
    {
        0 => Credential(start, end),
        1 => Data(start, end),
        2 => Pseudonymized(start, end),
        _ => Preserved(start, end),
    }, null));

    private static Gen<Material> WithIdentifiers(bool split) => Gen.Select(
        Gen.Select(SyntheticIdentifiers.Variants(split), Gen.Int[0, 2], Identifier).Array[1, 3],
        Gen.OneOf(Signal, Kept, Names).Array[0, 6],
        Gen.Int[0, int.MaxValue].Array[9],
        (identifiers, others, keys) => Built([.. identifiers.Concat(others).Select((segment, at) => (segment, keys[at])).OrderBy(pair => pair.Item2).Select(pair => pair.segment)]));

    private static readonly Gen<Material> NothingToRemove = Gen.OneOf(Signal, Kept, KeptNames).Array[1, 8].Select(Built);

    private static readonly IdentifierStep Removal = new(Boundaries);

    private static Sanitizer EveryStep() => Sanitizer([.. StepConformance.Registered()]);

    [Fact]
    [Trait("Kind", "Property")]
    public void ClosedFormIdentifierLeavesNoRecognizableVariant() =>
        PropertyRunner.FromProcess().Check(WithIdentifiers(split: false), material => LeavesNoResidue(EveryStep(), material));

    [Fact]
    [Trait("Kind", "Property")]
    public void IdentifierSplitByInvisibleCharacterLeaves() =>
        PropertyRunner.FromProcess().Check(WithIdentifiers(split: true), material => LeavesNoResidue(EveryStep(), material));

    [Fact]
    [Trait("Kind", "Property")]
    public void SanitizingTwiceEqualsOnce() =>
        PropertyRunner.FromProcess().Check(Gen.OneOf(WithIdentifiers(split: false), WithIdentifiers(split: true), NothingToRemove), material =>
        {
            var once = Sanitized(EveryStep(), material.Text, material.Decided);

            var twice = Sanitized(EveryStep(), once.Text, [.. once.Pseudonyms.Select(occurrence => new DecidedOccurrence(occurrence, Destination.Pseudonymize))]);

            Assert.Equal(once.Text, twice.Text);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TextWithNothingToRemoveLeavesIdentical() =>
        PropertyRunner.FromProcess().Check(NothingToRemove, material => Assert.Equal(material.Text, Sanitized(EveryStep(), material.Text, material.Decided).Text));

    [Fact]
    public void ResidueCheckRejectsARemovalStepWithAPlantedDefect()
    {
        var naive = new FixedStep(Removal.Name, input => [.. Removal.Edits(input).Where(edit => !HoldsAnInvisible(Cut(input.Text, edit.Start, edit.End)))]);
        var defective = Sanitizer([.. StepConformance.Registered().Select(step => step.Name == Removal.Name ? naive : step)]);

        var failure = Record.Exception(() => PropertyRunner.FromProcess().Check(WithIdentifiers(split: true), material => LeavesNoResidue(defective, material)));

        var property = Assert.IsType<CsCheckException>(failure);
        _ = Assert.IsType<AllException>(property.InnerException);
    }

    /// <summary>No planted identifier is in the sanitized text: its digits, joined across whatever is not a letter, nor its letters and digits together, in ASCII capitals.</summary>
    private static void LeavesNoResidue(Sanitizer sanitizer, Material material)
    {
        var sanitized = Sanitized(sanitizer, material.Text, material.Decided);
        var numbers = ResidueOf(sanitized.Text, letters: false);
        var whole = ResidueOf(sanitized.Text, letters: true);

        Assert.All(material.Skeletons, skeleton => Assert.DoesNotContain(skeleton, skeleton.Any(char.IsAsciiLetter) ? whole : numbers, StringComparison.Ordinal));
    }

    /// <summary>The digits in ASCII and, when asked, the letters in capitals; without letters, each letter becomes a bar, which breaks the run of digits it stands in.</summary>
    private static string ResidueOf(string text, bool letters)
    {
        var skeleton = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (!SyntheticIdentifiers.Invisibles.Contains(rune.ToString()))
            {
                _ = skeleton.Append(Rune.IsLetter(rune) && !letters ? "|" : SkeletonOf(rune));
            }
        }

        return skeleton.ToString();
    }

    /// <summary>Whether the cut holds one of the invisible characters the identifiers are split by, as the generator writes them and not as the removal reads them.</summary>
    private static bool HoldsAnInvisible(string cut) => Array.Exists(SyntheticIdentifiers.Invisibles, invisible => cut.Contains(invisible, StringComparison.Ordinal));

    /// <summary>A digit in ASCII, a letter in capitals, and nothing for the rest.</summary>
    private static string SkeletonOf(Rune rune) => rune switch
    {
        _ when Rune.IsDigit(rune) || IsDigitOfOtherNumber(rune) => ((int)Rune.GetNumericValue(rune)).ToString(CultureInfo.InvariantCulture),
        _ when Rune.IsLetter(rune) => rune.ToString().ToUpperInvariant(),
        _ => string.Empty,
    };

    /// <summary>A number of category No with the value of one digit, as the circled and the superscript ones, which reads as a digit.</summary>
    private static bool IsDigitOfOtherNumber(Rune rune) =>
        Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherNumber && Rune.GetNumericValue(rune) is >= 0 and <= 9 && double.IsInteger(Rune.GetNumericValue(rune));

    private static Segment Plain(string written) => new(written, null, null);

    private static Segment Money(long whole, int cents, string currency) => Plain(string.Create(CultureInfo.InvariantCulture, $"{currency}{Grouped(whole)},{cents:00}"));

    private static Segment Date(int day, int month, int year, int hour, int minute, int shape) => Plain(shape switch
    {
        0 => string.Create(CultureInfo.InvariantCulture, $"{day:00}/{month:00}/{year}"),
        1 => string.Create(CultureInfo.InvariantCulture, $"{year}-{month:00}-{day:00} {hour:00}:{minute:00}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{day}.{month}.{year} {hour}:{minute:00}"),
    });

    private static Segment Identifier(Planted planted, int marked) => new(planted.Written, marked switch
    {
        0 => null,
        1 => Data,
        _ => (start, _) => Data(start, start + 2),
    }, planted.Skeleton);

    private static string Grouped(long amount)
    {
        var digits = amount.ToString(CultureInfo.InvariantCulture);
        var head = ((digits.Length - 1) % 3) + 1;
        return digits[..head] + string.Concat(Enumerable.Range(0, (digits.Length - head) / 3).Select(group => string.Concat(".", digits.AsSpan(head + (group * 3), 3))));
    }

    private static Material Built(Segment[] segments)
    {
        var text = new StringBuilder();
        var decided = new List<DecidedOccurrence>();
        foreach (var segment in segments)
        {
            _ = text.Append(text.Length > 0 ? Joiner : string.Empty);
            var start = Boundaries.CodePointCount(text.ToString());
            _ = text.Append(segment.Text);
            decided.AddRange(segment.Marked is { } marked ? [marked(start, start + Boundaries.CodePointCount(segment.Text))] : []);
        }

        return new Material(text.ToString(), [.. decided], [.. segments.Select(segment => segment.Skeleton).OfType<string>()]);
    }

    /// <summary>A piece of the material: its text, the occurrence the entrance marked on it, if any, and the skeleton of the identifier it is, if it is one.</summary>
    private sealed record Segment(string Text, Func<int, int, DecidedOccurrence>? Marked, string? Skeleton);

    /// <summary>A generated material: the text, the occurrences decided on it and the skeletons of the identifiers planted in it.</summary>
    private sealed record Material(string Text, DecidedOccurrence[] Decided, string[] Skeletons);
}
