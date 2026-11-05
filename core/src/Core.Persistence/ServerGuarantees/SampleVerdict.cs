namespace Core.Persistence.ServerGuarantees;

/// <summary>What the sample of sealed records concluded.</summary>
internal enum SampleVerdict
{
    /// <summary>Every record of the sample opened, or there was none to open.</summary>
    Opened,

    /// <summary>A record outside the scope of a declared loss did not open: the guarantee is missing.</summary>
    NotOpened,

    /// <summary>Every record of the domain is under the scope of a declared loss: information, not a guarantee missing.</summary>
    NoneOutsideLoss,

    /// <summary>
    /// The search stopped at its limit of records under the scope of a declared loss before it found one outside: nothing was opened,
    /// and it is not known whether one is readable further on.
    /// </summary>
    SearchStopped,
}
