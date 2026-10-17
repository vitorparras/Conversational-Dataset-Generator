using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Gives generators the options the merged configs hold, as the compiler would.</summary>
internal sealed class ConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigSet configs;

    public ConfigOptionsProvider(AnalyzerConfigSet configs)
    {
        this.configs = configs;
    }

    public override AnalyzerConfigOptions GlobalOptions => new Options(configs.GlobalConfigOptions.AnalyzerOptions);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => OptionsOf(tree.FilePath);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => OptionsOf(textFile.Path);

    private Options OptionsOf(string path) => new(configs.GetOptionsForSourcePath(path).AnalyzerOptions);

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> values;

        public Options(IReadOnlyDictionary<string, string> values)
        {
            this.values = values;
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }
}
