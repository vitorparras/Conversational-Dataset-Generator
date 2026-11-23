# Fatia C — pedido e autorização da reversão

> Norma pendente. 2026-11-22. Fecha #103, #104 (menos CA-2 e CA-3, da fatia D), #614 e #615. Teto
> da fatia: 1.400 linhas de produção.

A Curadoria abre `/reversao/pedir` (R1) e pede a reversão de um marcador num item, escrevendo por
que a análise não avança sem a identidade, o que fará com ela e por quanto tempo. O escopo é um
marcador e um item: o pedido confere o marcador contra os marcadores do item, em claro, sem abrir o
mapa. Outra pessoa da Curadoria do mesmo domínio vê em `/reversao/autorizar` (R2) os pedidos
pendentes dos outros, sem identidade, e autoriza ou recusa numa página própria, como no
esvaziamento (`EmptyingScreensEndpoint`); a autorização declara até quando vale. Pedido incompleto
volta com os erros ligados aos campos ([0051](../../../decisoes/0051-navegacao-gestao-e-relatorio.md)).

Quem não é da Curadoria recebe a página de endereço inexistente (`ScreenGate`), e a tentativa vira
evento de recusa com quem, quando e qual escopo (#103 CA-3). Renovar é autorizar de novo o mesmo
pedido depois que a anterior venceu. Os protótipos são `pedido.html` e `autorizacao.html`, em
[protótipos](prototipos/index.html).

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | RequestWithoutReasonUseOrTermIsRefusedAndCreatesNothing (#103 CA-1); RequestReachingMoreThanOneMarkerOrItemIsRefusedByScope (#103 CA-2) |
| `Core.Persistence.Content.Tests` | RefusalIsItsOwnEventAndCannotBeRemovedOrChanged (#103 CA-4); AuthorizingOwnRequestIsRefusedAndRecorded (#104 CA-1, na aplicação e no gatilho, um teste cada); RenewalIsANewAuthorizationWithWhoWhenAndTerm (#104 CA-4); BrokenChainRefusesNewAuthorizationsInTheDomain (#104 CA-5); SecondAuthorizationWhileOneIsValidIsRefused; AuthorizationWithATermInThePastIsRefused; RequestRowsCannotBeChanged |
| `Core.Host.Tests` | MarkerNotInTheItemIsRefusedByScopeWithTheReason (#103 CA-2); AnnotatorRequestIsRefusedAndRecordedWithWhoWhenAndScope (#103 CA-3); AuthorizationByWhoIsNotCurationOfTheDomainIsRefused (#104); IncompleteRequestFormReturnsWithTheErrorsAndAsksNothing (#614 CA-1); DomainWideRequestIsRefusedOnTheScreenAndRecorded (#614 CA-2); AnnotatorOpeningTheRequestPageGetsNotFound (#614 CA-3); SentRequestShowsWhoIsMissingAndNoIdentity (#614 CA-4); OwnRequestIsNotListedAndDirectPostIsRefused (#615 CA-1); PendingRequestShowsReasonUseAndTermAndNoIdentity (#615 CA-2); AuthorizationIsConfirmedOnItsOwnPageForThatRequestOnly (#615 CA-3); DeclineIsRecordedWithWhoWhenAndScope (#615 CA-4) |
| `Core.Host.Tests.Browser` | RequestFormIsFilledByKeyboardAndPassesTheAccessibilityCheck; AuthorizationIsConfirmedByKeyboardAndPassesTheAccessibilityCheck |

"Domínio inteiro" e "conjunto sem critério" (#614 CA-2) chegam pelo formulário como mais de um
marcador ou item, ou como nenhum: a tela só aceita um de cada, e o POST montado à mão com mais é
recusado pelo escopo, com o evento. O CA-3 da #614 tem dois testes: o endereço responde como
inexistente, e a política nega mesmo com a rota chamada direto.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | a reversal-request em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Pedido e autorização | uma pasta nova de reversão em `core/src/Core.Ingestion` | [002](../../padroes/002-servico-de-dominio.md) |
| Armazenamento | um novo ao lado de `ContentWriter`: pedir, autorizar, recusar, listar | [001](../../padroes/001-armazenamento.md) |
| Rotas | um endpoint novo, como `RerunScreensEndpoint`, e `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Telas | R1 e R2, com os estados do protótipo | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** duas autorizações ao mesmo tempo e cadeia quebrada, nas linhas da fatia C em
  [riscos](riscos.md).
- **Raia:** a pasta de reversão, a tabela nova, as rotas e as telas R1 e R2. Não abre o mapa.
- **Depende de:** a fatia B, pelos marcadores do item. **Bloqueia:** a fatia D.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
