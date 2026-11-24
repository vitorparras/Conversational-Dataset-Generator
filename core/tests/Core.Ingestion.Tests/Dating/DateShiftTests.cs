using System.Globalization;
using Core.Ingestion.Contracts.Dating;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Dating;

/// <summary>
/// The shift of the dates of a domain (issue 108): whole days, never zero, the same quantity for every moment so that each interval stays exact, and a row that does not open
/// reported as lost for good instead of guessed or drawn again.
/// </summary>
public sealed class DateShiftTests
{
    private static readonly long Earliest = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;
    private static readonly long Latest = new DateTimeOffset(2200, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

    private static readonly Gen<DateShift> AnyShift =
        Gen.Int[-DateShift.MostDays, DateShift.MostDays - 1].Select(days => DateShift.Read((days >= 0 ? days + 1 : days).ToString(CultureInfo.InvariantCulture))!);

    private static readonly Gen<DateTimeOffset> AnyMoment =
        Gen.Select(Gen.Long[Earliest, Latest], Gen.Int[-14, 14]).Select(generated => new DateTimeOffset(generated.Item1, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(generated.Item2)));

    [Fact]
    [Trait("Kind", "Property")]
    public void ShiftKeepsEveryIntervalExact() =>
        PropertyRunner.FromProcess().Check(Gen.Select(AnyShift, AnyMoment, AnyMoment), generated =>
        {
            var (shift, first, second) = generated;

            var interval = shift.Apply(second) - shift.Apply(first);

            Assert.Equal((second - first).Ticks, interval.Ticks);
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void ShiftMovesEveryMomentByItsWholeDays() =>
        PropertyRunner.FromProcess().Check(Gen.Select(AnyShift, AnyMoment), generated =>
        {
            var (shift, moment) = generated;

            var moved = shift.Apply(moment);

            Assert.Equal((shift.Days * TimeSpan.TicksPerDay, moment.Offset), ((moved - moment).Ticks, moved.Offset));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void DrawnShiftIsNeverZeroAndNeverBeyondTheMostDays() =>
        PropertyRunner.FromProcess().Check(Gen.Int[0, 1], _ =>
        {
            var drawn = DateShift.Draw();

            Assert.True(drawn.Days != 0 && Math.Abs(drawn.Days) <= DateShift.MostDays, $"drawn {drawn.Days}");
        });

    [Fact]
    public void LostShiftIsReportedAsDefinitive()
    {
        StoredDateShift[] lost = [StoredDateShift.Unopened, StoredDateShift.Opened("zqx"), StoredDateShift.Opened("0"), StoredDateShift.Opened("3651"), StoredDateShift.Opened("-2147483648")];

        var readings = lost.Select(DateShiftRule.Establish).Select(reading => (reading.Outcome, reading.Shift)).ToList();

        Assert.All(readings, reading => Assert.Equal((DateShiftOutcome.Lost, null), reading));
    }

    [Fact]
    public void DomainWithNoRowDrawsAShift()
    {
        var reading = DateShiftRule.Establish(StoredDateShift.None);

        Assert.Equal((DateShiftOutcome.Drawn, true), (reading.Outcome, reading.Shift is { Days: not 0 }));
    }

    [Fact]
    public void RowThatOpensGivesTheShiftItKeeps()
    {
        var reading = DateShiftRule.Establish(StoredDateShift.Opened("-417"));

        Assert.Equal((DateShiftOutcome.Kept, -417), (reading.Outcome, reading.Shift?.Days));
    }

    [Fact]
    public void DrawnShiftsSpreadOverBothSignsAndTheWholeRange()
    {
        var days = Enumerable.Range(0, 2000).Select(_ => DateShift.Draw().Days).ToList();

        Assert.Equal((true, true, true), (days.Exists(day => day < 0), days.Exists(day => day > 0), days.Exists(day => Math.Abs(day) > DateShift.MostDays / 2)));
        Assert.InRange(days.Distinct().Count(), 1500, 2000);
    }

    [Theory]
    [InlineData(-3650, -3650)]
    [InlineData(-1, -1)]
    [InlineData(0, 1)]
    [InlineData(3649, 3650)]
    public void EveryDrawnNumberBecomesAShiftOfWholeDaysAndZeroNeverComes(int drawn, int days) =>
        Assert.Equal(days, DateShift.FromDrawn(drawn).Days);

    [Theory]
    [InlineData("3650", 3650)]
    [InlineData("-3650", -3650)]
    [InlineData("1", 1)]
    public void ShiftUpToTheMostDaysEitherWayIsReadBack(string stored, int days) =>
        Assert.Equal(days, DateShift.Read(stored)?.Days);

    [Fact]
    public void EstablishingWithoutTheRowIsADefectOfTheCaller() =>
        Assert.Equal("stored", Assert.Throws<ArgumentNullException>(() => DateShiftRule.Establish(null!)).ParamName);

    [Fact]
    public void ShiftWrittenAsTextTellsNothingOfItsDays()
    {
        var shift = DateShift.Read("2917")!;

        var written = $"{shift} {DateShiftRule.Establish(StoredDateShift.Opened("2917"))}";

        Assert.DoesNotContain("2917", written, StringComparison.Ordinal);
    }
}
