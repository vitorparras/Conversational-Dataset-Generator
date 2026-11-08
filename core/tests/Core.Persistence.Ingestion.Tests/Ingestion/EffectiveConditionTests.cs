using Core.Persistence.Ingestion;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The condition of a set of items that bring different ones is the most restrictive among them, whatever the proportion (issue 85, CA-3): a pure function of the conditions present, which
/// does not depend on the order nor on the count of the items and is always one of them.
/// </summary>
public sealed class EffectiveConditionTests
{
    private const int MostConditions = 8;
    private const int MostItems = 12;
    private const int MostDilution = 60;

    /// <summary>
    /// An installation that knows from one to eight conditions, and a non-empty set of items, each with the place of its condition in the list;
    /// the keys give the order a permutation takes.
    /// </summary>
    private static readonly Gen<(int Size, int[] Ranks, int[] Keys)> Sets = Gen.Int[1, MostConditions].SelectMany(size =>
        Gen.Int[0, size - 1].Array[1, MostItems].SelectMany(ranks => Gen.Int.Array[ranks.Length].Select(keys => (size, ranks, keys))));

    private static readonly OriginConditions Known = OriginConditions.Of(["restrita", "intermediaria", "permissiva"]);

    [Fact]
    [Trait("Kind", "Property")]
    public void TheResultIsAlwaysTheMostRestrictiveOfTheConditionsPresentAndOneOfThem() =>
        PropertyRunner.FromProcess().Check(Sets, generated =>
        {
            var (size, ranks, _) = generated;
            var order = Order(size);
            var present = ranks.Select(rank => order.Names[rank]).ToArray();

            var effective = EffectiveCondition.Of(order, present);

            Assert.Equal(order.Names[ranks.Min()], effective);
            Assert.Contains(effective, present);
            Assert.All(present, name => Assert.True(order.RankOf(effective) <= order.RankOf(name)));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheOrderOfTheItemsDoesNotChangeTheResult() =>
        PropertyRunner.FromProcess().Check(Sets, generated =>
        {
            var (size, ranks, keys) = generated;
            var order = Order(size);
            var present = ranks.Select(rank => order.Names[rank]).ToArray();
            var shuffled = Enumerable.Range(0, present.Length).OrderBy(at => keys[at]).ThenBy(at => at).Select(at => present[at]).ToArray();

            Assert.Equal(EffectiveCondition.Of(order, present), EffectiveCondition.Of(order, shuffled));
        });

    [Fact]
    [Trait("Kind", "Property")]
    public void TheCountOfTheItemsDoesNotChangeTheResultNoMatterHowManyOfTheMostPermissiveDiluteTheSet() =>
        PropertyRunner.FromProcess().Check(Sets.SelectMany(set => Gen.Int[0, MostDilution].Select(extra => (set.Size, set.Ranks, Extra: extra))), generated =>
        {
            var (size, ranks, extra) = generated;
            var order = Order(size);
            var present = ranks.Select(rank => order.Names[rank]).ToArray();
            var diluted = present.Concat(Enumerable.Repeat(order.Names[size - 1], extra)).ToArray();
            var repeated = present.Concat(present).Concat(present).ToArray();

            var alone = EffectiveCondition.Of(order, present);

            Assert.Equal(alone, EffectiveCondition.Of(order, diluted));
            Assert.Equal(alone, EffectiveCondition.Of(order, repeated));
            Assert.Equal(alone, EffectiveCondition.Of(order, present.Distinct(StringComparer.Ordinal)));
        });

    [Fact]
    public void OneItemOfTheMostRestrictiveAmongAHundredOfTheMostPermissiveGivesTheMostRestrictive()
    {
        var present = Enumerable.Repeat("permissiva", 100).Append("restrita").Concat(Enumerable.Repeat("intermediaria", 100));

        Assert.Equal("restrita", EffectiveCondition.Of(Known, present));
    }

    [Fact]
    public void ASetOfOneItemGivesTheConditionOfThatItem()
    {
        var order = new[] { "permissiva", "intermediaria", "restrita" };

        Assert.Equal(order, order.Select(name => EffectiveCondition.Of(Known, new[] { name })));
    }

    [Fact]
    public void ThePlaceInTheListIsWhatOrdersAndNotTheAlphabetNorTheNames()
    {
        var reversed = OriginConditions.Of(["z_primeira", "m_segunda", "a_terceira"]);

        Assert.Equal("z_primeira", EffectiveCondition.Of(reversed, ["a_terceira", "z_primeira", "m_segunda"]));
        Assert.Equal("m_segunda", EffectiveCondition.Of(reversed, ["a_terceira", "m_segunda"]));
    }

    [Fact]
    public void ASetWithNoItemHasNoConditionAndSaysSo()
    {
        var refused = Assert.Throws<ArgumentException>(() => EffectiveCondition.Of(Known, []));

        Assert.Equal("present", refused.ParamName);
    }

    [Fact]
    public void AConditionTheInstallationDoesNotKnowIsRefusedWhereverItIsInTheSet()
    {
        var first = Assert.Throws<ArgumentException>(() => EffectiveCondition.Of(Known, ["zqx_desconhecida", "restrita"]));
        var last = Assert.Throws<ArgumentException>(() => EffectiveCondition.Of(Known, ["restrita", "zqx_desconhecida"]));

        Assert.Equal(("present", "present"), (first.ParamName, last.ParamName));
    }

    [Fact]
    public void MissingArgumentsAreRefusedByName() =>
        Assert.Equal(
            ["order", "present"],
            [
                Assert.Throws<ArgumentNullException>(() => EffectiveCondition.Of(null!, ["restrita"])).ParamName,
                Assert.Throws<ArgumentNullException>(() => EffectiveCondition.Of(Known, null!)).ParamName,
            ]);

    private static OriginConditions Order(int size) => OriginConditions.Of([.. Enumerable.Range(0, size).Select(rank => $"c{rank}")]);
}
