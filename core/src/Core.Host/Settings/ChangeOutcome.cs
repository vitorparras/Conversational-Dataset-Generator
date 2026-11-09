namespace Core.Host.Settings;

/// <summary>How a submission of a change of configuration ended; only the first wrote anything.</summary>
internal enum ChangeOutcome
{
    /// <summary>The submission and its event were written, and it waits for the approval of another person.</summary>
    Recorded,

    /// <summary>The motive is not one of the closed list.</summary>
    MotiveUnknown,

    /// <summary>The key is not one the screen changes; it changes only in the file, on the server.</summary>
    OnlyOnServer,

    /// <summary>The value in force comes from the environment or from an argument, which the file does not override, so the change would not take effect.</summary>
    OverriddenOnServer,

    /// <summary>The configuration that would result is one the start refuses; the problems name the key and the value.</summary>
    ValueRefused,

    /// <summary>Whoever submits is not an active person with the technical administration.</summary>
    WithoutFunction,

    /// <summary>The chain refused the event, so nothing was written.</summary>
    NotRecorded,
}
