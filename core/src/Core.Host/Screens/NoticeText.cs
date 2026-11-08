using System.Globalization;
using Core.Persistence.Notices;

namespace Core.Host.Screens;

/// <summary>The words of each notice, fixed by its code: nothing a person typed and no detail of the chain reaches a notice, only the code and the number the act ended.</summary>
internal static class NoticeText
{
    internal static string Of(PersonNotice notice) => notice.Code switch
    {
        NoticeCode.PasswordReset => PasswordReset(notice.SessionsEnded ?? 0),
        NoticeCode.DomainReadOnly => "A escrita nos domínios foi suspensa porque a cadeia de custódia não conferiu na última verificação. A leitura continua.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice), notice.Code, "The notice has a code the screen has no words for."),
    };

    private static string PasswordReset(int ended) => "Sua senha foi reposta por quem administra o sistema. " + (ended switch
    {
        0 => "Nenhuma sessão estava aberta.",
        1 => "1 sessão aberta foi encerrada.",
        _ => string.Create(CultureInfo.InvariantCulture, $"{ended} sessões abertas foram encerradas."),
    });
}
