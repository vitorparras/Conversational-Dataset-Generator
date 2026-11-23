# Fatia C — pedido e autorização da reversão

> Norma pendente. 2026-11-22. Fecha #103, #104 (menos CA-2 e CA-3, da fatia D), #614 e #615. Teto
> da fatia: 1.400 linhas de produção.

A Curadoria abre `/reversao/pedir` (R1) e pede a reversão de um marcador num item: por que a
análise não avança sem a identidade, o que fará com ela e por quanto tempo. O escopo é um marcador e
um item, conferido contra os marcadores do item, em claro, sem abrir o mapa. O envio leva à página
do pedido em `/reversao/meus-pedidos/{id}`, que diz quem falta. Outra pessoa da Curadoria do mesmo
domínio vê em `/reversao/autorizar` (R2) os pendentes dos outros, sem identidade, com filtro e
página no endereço, e decide numa página própria, como no esvaziamento (`EmptyingScreensEndpoint`);
a autorização declara até quando vale. Formulário incompleto volta com os erros ligados aos campos
([0051](../../../decisoes/0051-navegacao-gestao-e-relatorio.md)).

Quem não é da Curadoria recebe a página de endereço inexistente (`ScreenGate`), e a tentativa vira
evento de recusa (#103 CA-3). Renovar é autorizar de novo o mesmo pedido depois que a anterior
venceu. Com a cadeia quebrada, nenhum ato é gravado, nem a recusa; a tela diz por quê. Os
protótipos são `pedido.html`, `autorizacao.html` e a lista de `meus-pedidos.html`, em
[protótipos](prototipos/index.html), e seguem a [0058](../../../decisoes/0058-padrao-de-usabilidade-e-visual.md).
Na R1, o botão de enviar fica visível no celular (390x844) sem rolar, ou o mais perto disso: é
critério da implementação, conferido na captura.

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | RequestWithoutReasonUseOrTermIsRefused (#103 CA-1); RequestReachingMoreThanOneMarkerOrItemIsRefusedByScope (#103 CA-2) |
| `Core.Persistence.Content.Tests` | IncompleteRequestWritesNoRowAndNoEvent (#103 CA-1); RefusalIsItsOwnEventAndCannotBeRemovedOrChanged (#103 CA-4); EachReversalStepPointsToItsListedKindAndReference; RefusalReferenceCarriesNoText; OwnAuthorizationIsRefusedByTheDatabase (#104 CA-1); RenewalIsANewAuthorizationWithWhoWhenAndTerm (#104 CA-4); BrokenChainRefusesNewAuthorizations (#104 CA-5); SecondAuthorizationWhileOneIsValidIsRefused; AuthorizationWithATermInThePastIsRefused; RequestRowsCannotBeChanged; DatabaseCopyHasNoReadableRequestText |
| `Core.Host.Tests` | MarkerNotInTheItemIsRefusedByScopeWithTheReason (#103 CA-2); AnnotatorRequestIsRefusedAndRecordedWithWhoWhenAndScope (#103 CA-3); AuthorizingOwnRequestIsRefusedByTheApplicationAndRecorded (#104 CA-1); AuthorizationByWhoIsNotCurationOfTheDomainIsRefused (#104); IncompleteRequestFormReturnsWithTheErrorsAndAsksNothing (#614 CA-1); DomainWideRequestIsRefusedOnTheScreenAndRecorded (#614 CA-2); AnnotatorOpeningTheRequestPageGetsNotFound e AnnotatorPostingTheRequestIsDeniedByThePolicy (#614 CA-3); SentRequestShowsWhoIsMissingAndNoIdentity (#614 CA-4); OwnRequestIsNotListedForApproval e DirectPostOnOwnRequestIsRefused (#615 CA-1); PendingRequestShowsReasonUseAndTermAndNoIdentity (#615 CA-2); ReadingTheRequestTextIsRecordedBeforeOpening; AuthorizationIsConfirmedOnItsOwnPageForThatRequestOnly e AuthorizationPastItsTermIsShownExpired (#615 CA-3); DeclineIsRecordedWithWhoWhenAndScope (#615 CA-4); MissingOrOtherDomainRequestAnswersNotFound; ListFilterAndPageStayInTheAddress |
| `Core.Host.Tests.Browser` | RequestFormIsFilledByKeyboardAndPassesTheAccessibilityCheck; AuthorizationIsConfirmedByKeyboardAndPassesTheAccessibilityCheck |

"Domínio inteiro" e "conjunto sem critério" (#614 CA-2) chegam como mais de um marcador ou item, ou
nenhum: a tela só aceita um de cada, e o POST montado à mão com mais é recusado pelo escopo, com o
evento. O que cada recusa aponta (item, marcador ou classe do escopo amplo) está em
[eventos da reversão](eventos-da-reversao.md).

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | a reversal-request em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Pedido e autorização | uma pasta nova de reversão em `core/src/Core.Ingestion` | [002](../../padroes/002-servico-de-dominio.md) |
| Armazenamento | um novo ao lado de `ContentWriter`: pedir, autorizar, recusar, listar | [001](../../padroes/001-armazenamento.md) |
| Rotas | um endpoint novo, como `RerunScreensEndpoint`, e `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Telas | R1, R2 e a lista e a página do pedido da R3 | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** duas autorizações ao mesmo tempo, cadeia quebrada e cópia do banco, nas linhas da
  fatia C em [riscos](riscos.md).
- **Raia:** a pasta de reversão, a tabela nova, as rotas e as telas. Não abre o mapa; abre só o
  texto do pedido, com o evento de leitura.
- **Depende de:** a fatia B, pelos marcadores do item. **Bloqueia:** a fatia D.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
