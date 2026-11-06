namespace Core.Ingestion;

/// <summary>Why the conference refused the whole batch; one code for each refusal, and the findings say where.</summary>
public enum RefusalCode
{
    /// <summary>The reader could not read the material, or read only a part of it.</summary>
    Unreadable = 0,

    /// <summary>The material has no unit at all.</summary>
    NoUnits = 1,

    /// <summary>The form declared needs something a unit does not bring.</summary>
    FieldMissing = 2,

    /// <summary>A unit brings something the form declared does not have.</summary>
    FieldNotAllowed = 3,

    /// <summary>Two messages carry the same moment and nothing in the content, as the answer of one to the other, puts one before the other, so the order would come from the file.</summary>
    OrderNotDetermined = 4,

    /// <summary>A passage starts before the text, ends past it or has no length.</summary>
    PassageOutsideText = 5,

    /// <summary>The form declared is of one unit and the material has more.</summary>
    MoreThanOneUnit = 6,

    /// <summary>An answer has a moment before the message it answers, or the answers go round in a cycle, so the content contradicts itself about the order.</summary>
    OrderIncoherent = 7,
}
