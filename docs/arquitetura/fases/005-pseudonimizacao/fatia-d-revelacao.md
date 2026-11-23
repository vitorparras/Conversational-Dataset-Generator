# Fatia D — registro antes de revelar

> Norma pendente. 2026-11-22. Fecha #105, #616 e os CA-2 e CA-3 da #104. Teto da fatia: 900
> linhas de produção.

Quem pediu abre `/reversao/meus-pedidos` (R3) e vê os próprios pedidos: pendente e quem falta,
recusado, autorizado até quando, vencido ou revelado. Num pedido autorizado e dentro do prazo, a
página do pedido oferece revelar. O POST grava a revelação e o evento reversal_revealed e confirma
a transação; só então abre a entrada do mapa do item e responde a página com a identidade, uma vez,
sem cache. Recarregar, voltar ou abrir o pedido de novo mostra só "revelado em", nunca o valor.

A revelação é de um marcador por vez, e cada uma é um evento. A autorização vale só para o pedido
dela: a chave estrangeira já impede usá-la em outro, e a revelação única impede usá-la duas vezes.
Item esvaziado não tem mais mapa: a revelação é recusada com o motivo. O protótipo é
`meus-pedidos.html`, em [protótipos](prototipos/index.html).

**Ordem que o teste prova (#105 CA-1).** O teste injeta, entre a confirmação do evento e a leitura
do mapa, uma leitura da cadeia por outra conexão, e reprova se o valor sair antes de o evento estar
lá. Para o CA-2, o teste derruba a gravação do evento e confere que o mapa não foi aberto.

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Persistence.Content.Tests` | RevelationEventIsInTheChainBeforeTheValueLeaves (#105 CA-1); FailureToRecordTheEventRevealsNothingAndLeavesTheRequestOpen (#105 CA-2); RevelationEventCannotBeRemovedOrChangedAndTheAttemptIsDetected (#105 CA-3); TwoRevelationsAreTwoEventsEachWithItsMarker (#105 CA-5); ExpiredAuthorizationDeniesAsIfItNeverExistedAndIsRecorded (#104 CA-2); AuthorizationServesOnlyItsOwnRequest (#104 CA-3); TwoRevealPostsAtOnceRevealOnce |
| `Core.Host.Tests` | RevealedValueIsInNoTableLogOrCache (#105 CA-4, semeado); RevealPostRecordsBeforeTheIdentityAppears (#616 CA-1); RevealedPageIsNotCachedAndIsNotShownAgain (#616 CA-2); ExpiredOrAlreadyRevealedRequestIsRefused (#616 CA-3, um teste por caso); RecordAndLogCarryNoRevealedIdentity (#616 CA-4); MyRequestsShowStateAndValidity; RevealOfAnEmptiedItemIsRefusedWithTheReason; OthersCannotOpenMyRequest |
| `Core.Host.Tests.Browser` | RevelationIsReadByKeyboardAndPassesTheAccessibilityCheck |

O CA-4 da #105 semeia um original, revela e procura o valor no despejo do banco, nas linhas do log
e nas respostas seguintes do mesmo pedido. O CA-2 da #616 confere o cabeçalho sem cache da resposta
do POST e que o GET do pedido não traz o valor.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | a reversal-revelation em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Revelação | a pasta de reversão da fatia C | [002](../../padroes/002-servico-de-dominio.md) |
| Leitura do mapa | `ProtectedContentReader`, depois do evento | [001](../../padroes/001-armazenamento.md) |
| Rotas e tela | R3 no endpoint da fatia C, `ScreenPaths`, `ScreenResult` | [003](../../padroes/003-rota.md), [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** POST repetido, evento que falha, prazo vencido e identidade guardada, nas linhas da
  fatia D em [riscos](riscos.md).
- **Raia:** a revelação, a tabela nova e a tela R3. Não muda o pedido nem a autorização.
- **Depende de:** a fatia C. **Bloqueia:** a fatia E.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
