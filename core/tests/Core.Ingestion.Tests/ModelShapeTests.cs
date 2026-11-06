using System.Reflection;

namespace Core.Ingestion.Tests;

/// <summary>
/// The shape of the model is fixed by test: the forms are four and sealed (decision 0005), no one else can derive a fifth,
/// and nothing a reader hands over or a form keeps can carry the name of the file or the place of the text on the page (CA-3).
/// </summary>
public sealed class ModelShapeTests
{
    private static readonly string[] Forbidden = ["File", "Path", "Page", "Position", "Line", "Column", "Row", "Order", "Index", "Offset"];

    private static readonly Type[] Model =
    [
        typeof(ReadMaterial), typeof(ReadUnit), typeof(Passage), typeof(Material), typeof(Conversation), typeof(Message), typeof(Comment),
        typeof(Document), typeof(LabeledSequence), typeof(LabeledText),
    ];

    [Fact]
    public void ThereAreExactlyFourFormsAndEachIsSealed()
    {
        var forms = typeof(Material).Assembly.GetTypes().Where(type => type != typeof(Material) && typeof(Material).IsAssignableFrom(type)).ToList();

        Assert.Equal(
            ["Comment", "Conversation", "Document", "LabeledSequence"],
            [.. forms.Select(form => form.Name).Order(StringComparer.Ordinal)]);
        Assert.All(forms, form => Assert.True(form.IsSealed, form.Name));
    }

    [Fact]
    public void TheEnumerationOfTheFormsHasTheSameFourAndNoMore()
    {
        Assert.Equal(
            ["Comment", "Conversation", "Document", "LabeledSequence"],
            [.. Enum.GetNames<MaterialForm>().Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void NoFormCanBeDerivedOutsideTheAssembly()
    {
        var constructors = typeof(Material).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.All(constructors, constructor => Assert.True(constructor.IsFamilyAndAssembly, constructor.ToString()));
        Assert.NotEmpty(constructors);
        Assert.True(typeof(Material).IsAbstract);
    }

    [Fact]
    public void EachFormAnswersForItsOwnFormAndNoOther()
    {
        var forms = new Material[]
        {
            new Conversation([]),
            new Comment("a", null, "t"),
            new Document([]),
            new LabeledSequence([]),
        };

        Assert.Equal(
            new[] { MaterialForm.Conversation, MaterialForm.Comment, MaterialForm.Document, MaterialForm.LabeledSequence },
            [.. forms.Select(form => form.Form)]);
    }

    [Fact]
    public void NothingTheModelCarriesIsNamedAfterTheFileOrThePlaceOnThePage()
    {
        var names = Model.SelectMany(NamesOf).ToList();

        var found = names.Where(name => Array.Exists(Forbidden, word => name.Contains(word, StringComparison.OrdinalIgnoreCase))).ToList();

        Assert.Empty(found);
        Assert.Contains("ReadUnit.Author", names);
    }

    private static IEnumerable<string> NamesOf(Type type)
    {
        var members = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(property => $"{type.Name}.{property.Name}");
        var parameters = type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => $"{type.Name}({parameter.Name})");
        return members.Concat(parameters);
    }
}
