using System.Reflection;
using System.Runtime.Loader;
using Core.Persistence.Custody.Chain;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The names are tables built once, when the type is first used, so a test that only reads them sees the tables of the first use of the process. Here the assembly is loaded a second time,
/// in a context of its own, so that its tables are built again and what they hold is what the code declares.
/// </summary>
public sealed class CustodyNamesFreshLoadTests
{
    private static readonly string[] Enumerations = ["CustodyItemKind", "CustodyStep", "CustodyReason"];

    private static readonly string[] Declared =
    [
        "person", "item_text", "reversal_map", "custody_chain", "content_epoch", "domain", "sign_in_attempt", "master_key",
        "person_created", "person_deactivated", "person_function_changed", "person_password_set", "correction",
        "raw_text_read", "sanitized_text_read", "reversal_entries_read", "verify_chain", "item_emptied", "item_emptying_refused", "epoch_rotated", "domain_created", "seal_recorded",
        "domain_label_rotated", "sign_in_accepted", "sign_in_refused", "credential_recomputed", "key_loss_declared", "recovery_tested",
        "administrative_act", "correction_of_record", "content_read", "verification_passed", "verification_failed",
        "removal_requested_by_subject", "contract_requirement", "author_without_function", "item_already_emptied", "credentials_accepted", "password_wrong", "account_not_recognized",
    ];

    [Fact]
    public void NamesOfAFreshLoadOfTheAssemblyAreTheOnesTheStoredEventsAreRecordedUnder()
    {
        var context = new AssemblyLoadContext("custody-names-fresh", isCollectible: true);
        try
        {
            var names = context.LoadFromAssemblyPath(typeof(CustodyChain).Assembly.Location).GetType("Core.Persistence.Custody.CustodyNames", throwOnError: true)!;

            var found = Enumerations.SelectMany(enumeration => NamesOf(names, enumeration));

            Assert.Equal<IEnumerable<string>>(Declared, found);
        }
        finally
        {
            context.Unload();
        }
    }

    private static IEnumerable<string> NamesOf(Type names, string enumeration)
    {
        var values = names.Assembly.GetType($"Core.Persistence.Custody.{enumeration}", throwOnError: true)!;
        var of = names.GetMethod("Of", BindingFlags.NonPublic | BindingFlags.Static, [values])!;
        return Enum.GetValues(values).Cast<object>().Select(value => (string)of.Invoke(null, [value])!);
    }
}
