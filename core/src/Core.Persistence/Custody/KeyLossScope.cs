using Core.Platform.Keys;

namespace Core.Persistence.Custody;

/// <summary>What the loss declared reaches: the whole master key, or one derivation label, and what was sealed under it.</summary>
public sealed record KeyLossScope
{
    private KeyLossScope(string text)
    {
        Text = text;
    }

    /// <summary>The text of the scope that is the whole master key.</summary>
    public static string MasterText { get; } = "master";

    /// <summary>The whole master key.</summary>
    public static KeyLossScope Master { get; } = new(MasterText);

    /// <summary>The scope as the chain keeps it: <c>master</c>, or the label.</summary>
    public string Text { get; }

    /// <summary>Reads the scope as written: <c>master</c> or a derivation label exactly as the custody file writes it; anything else is not a scope.</summary>
    /// <param name="text">The scope as written.</param>
    /// <param name="scope">The scope, when it was read.</param>
    public static bool TryRead(string? text, out KeyLossScope? scope)
    {
        scope = text == MasterText ? Master : null;
        if (scope is null && DerivationLabel.TryParse(text, out var label))
        {
            scope = new KeyLossScope(label.Text);
        }

        return scope is not null;
    }
}
