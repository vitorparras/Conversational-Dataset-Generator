using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Core.Platform.Configuration;

/// <summary>
/// Command-line arguments read strictly: an argument that is not <c>--Key=value</c> or
/// <c>--Key value</c> is a refusal, never silently ignored.
/// </summary>
internal sealed class StrictArgumentsSource : IConfigurationSource
{
    private readonly IReadOnlyList<string> arguments;

    public StrictArgumentsSource(IReadOnlyList<string> arguments)
    {
        this.arguments = arguments;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(arguments);

    private sealed class Provider : ConfigurationProvider
    {
        private readonly IReadOnlyList<string> arguments;

        public Provider(IReadOnlyList<string> arguments)
        {
            this.arguments = arguments;
        }

        public override void Load()
        {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
                {
                    throw Malformed(index);
                }

                var separator = argument.IndexOf('=', StringComparison.Ordinal);
                if (separator > 2)
                {
                    data[argument[2..separator]] = argument[(separator + 1)..];
                    continue;
                }

                if (separator == 2 || index + 1 == arguments.Count
                    || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw Malformed(index);
                }

                data[argument[2..]] = arguments[index + 1];
                index++;
            }

            Data = data;
        }

        // The argument itself is not echoed: it may carry a value that is a secret.
        private static FormatException Malformed(int index) =>
            new(string.Create(
                CultureInfo.InvariantCulture,
                $"o argumento na posição {index + 1} não tem a forma --Chave=valor nem --Chave valor"));
    }
}
