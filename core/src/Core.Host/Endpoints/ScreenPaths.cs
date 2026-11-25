using System.Globalization;

namespace Core.Host.Endpoints;

/// <summary>The routes of the pages a person walks through, written once so that the forms and the links of the screens and the routes that answer them cannot drift apart.</summary>
internal static class ScreenPaths
{
    internal const string SignIn = "/entrada";
    internal const string SignOut = "/saida";
    internal const string Home = "/";
    internal const string State = "/estado-do-sistema";
    internal const string Configuration = "/configuracao";
    internal const string ConfigurationChanges = "/configuracao/alteracoes";
    internal const string ConfigurationDecision = "/configuracao/alteracoes/decisao";
    internal const string Domains = "/dominios";
    internal const string Chain = "/custodia";
    internal const string ChainCheck = "/custodia/verificar";
    internal const string Seal = "/custodia/selo";
    internal const string SealIssue = "/custodia/selo/emitir";
    internal const string SealConfirm = "/custodia/selo/confirmar";
    internal const string Account = "/minha-conta";
    internal const string AccountChange = "/minha-conta/senha";
    internal const string AccountEmail = "/minha-conta/email";
    internal const string AccountEnd = "/minha-conta/sessoes/encerrar";
    internal const string AccountEndOthers = "/minha-conta/sessoes/encerrar-outras";
    internal const string Operators = "/pessoas";
    internal const string OperatorNew = "/pessoas/nova";
    internal const string OperatorEmail = "/pessoas/{id}/email";
    internal const string Notices = "/avisos";
    internal const string Emptying = "/esvaziamento";
    internal const string EmptyingConfirm = "/esvaziamento/confirmar";
    internal const string Batches = "/lotes";
    internal const string BatchNew = "/lotes/novo";
    internal const string BatchDetail = "/lotes/{id:long}";
    internal const string BatchDraw = "/lotes/{id:long}/amostra";
    internal const string BatchReview = "/lotes/{id:long}/revisao";
    internal const string BatchReviewMark = "/lotes/{id:long}/revisao/itens/{place:int}/revisado";
    internal const string BatchReviewApprove = "/lotes/{id:long}/revisao/aprovar";
    internal const string BatchReviewFinding = "/lotes/{id:long}/revisao/itens/{place:int}/achado";
    internal const string Rerun = "/sanitizacao";
    internal const string RerunConfirm = "/sanitizacao/reexecutar";
    internal const string RerunProgress = "/sanitizacao/reexecucoes/{id:long}";
    internal const string ReversalAsk = "/reversao/pedir";
    internal const string ReversalMine = "/reversao/meus-pedidos";
    internal const string ReversalMineOne = "/reversao/meus-pedidos/{id:long}";
    internal const string ReversalAuthorize = "/reversao/autorizar";
    internal const string ReversalDecide = "/reversao/autorizar/{id:long}";

    /// <summary>The address of the detail of a batch.</summary>
    internal static string BatchOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/lotes/{id}");

    /// <summary>The address of the review of the sample of a batch.</summary>
    internal static string BatchReviewOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/lotes/{id}/revisao");

    /// <summary>The address where the sample of a batch is drawn.</summary>
    internal static string BatchDrawOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/lotes/{id}/amostra");

    /// <summary>The address where a finding of personal data is told at a place of the sample.</summary>
    internal static string BatchFindingOf(long id, int place) => string.Create(CultureInfo.InvariantCulture, $"/lotes/{id}/revisao/itens/{place}/achado");

    /// <summary>The address where a request of the rerun is followed.</summary>
    internal static string RerunProgressOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/sanitizacao/reexecucoes/{id}");

    /// <summary>The address of a request of the person of the session, as whoever asked follows it.</summary>
    internal static string ReversalMineOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/reversao/meus-pedidos/{id}");

    /// <summary>The address where another person of the curation decides a request.</summary>
    internal static string ReversalDecideOf(long id) => string.Create(CultureInfo.InvariantCulture, $"/reversao/autorizar/{id}");
}
