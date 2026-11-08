using System.Globalization;

namespace Core.Ingestion.Entrance;

/// <summary>The identifier the system gives to a submission when it arrives: generated here, and the only thing that stands for the submission afterwards; the name it came with is not kept.</summary>
/// <param name="Value">The generated value.</param>
public readonly record struct SubmissionId(Guid Value)
{
    /// <summary>The value, as 32 hexadecimal digits.</summary>
    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}
