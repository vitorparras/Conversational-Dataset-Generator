using Core.Platform.Sessions;

namespace Core.Host.Operators;

/// <summary>
/// The services the entry and the change of the own password share: one delay for both, so that a refusal of either counts in the same place;
/// with no database there is nobody to change a password.
/// </summary>
internal static class AccountServices
{
    internal static IServiceCollection Add(IServiceCollection services) =>
        services
            .AddSingleton(provider => new SignInDelay(provider.GetRequiredService<TimeProvider>()))
            .AddSingleton(provider => provider.GetService<OperatorAdministration>() is { } people
                ? new PasswordChange(people.Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>())
                : null!);
}
