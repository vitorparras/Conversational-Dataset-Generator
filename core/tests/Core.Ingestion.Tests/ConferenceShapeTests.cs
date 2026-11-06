using System.Reflection;

namespace Core.Ingestion.Tests;

/// <summary>An accepted material comes out of the conference and out of nowhere else: nothing outside the assembly can make one, give a rule or hand a result that accepts.</summary>
public sealed class ConferenceShapeTests
{
    [Fact]
    public void TheResultThatAcceptsAndTheRuleAreNotPartOfWhatIsPublic()
    {
        var publicMethods = typeof(ConferenceResult).GetMethods(BindingFlags.Static | BindingFlags.Public).Select(method => method.Name).ToList();

        Assert.DoesNotContain("Accept", publicMethods);
        Assert.Contains("Refuse", publicMethods);
        Assert.False(typeof(IFormRule).IsVisible);
        var constructor = Assert.Single(typeof(FormConference).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal(["ITextBoundaries"], [.. constructor.GetParameters().Select(parameter => parameter.ParameterType.Name)]);
    }

    [Fact]
    public void NoPublicTypeOfTheAssemblyImplementsTheRule() =>
        Assert.DoesNotContain(typeof(Material).Assembly.GetTypes(), type => type.IsVisible && typeof(IFormRule).IsAssignableFrom(type));
}
