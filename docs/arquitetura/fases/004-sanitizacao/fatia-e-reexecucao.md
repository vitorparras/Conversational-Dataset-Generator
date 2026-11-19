# Fatia E — reexecução sobre o bruto, em segundo plano

> Norma pendente. 2026-11-15. Fecha #97. Teto da fatia: 1.500 linhas de produção, o máximo da
> seção 9 das [regras de código](../../../regras-de-codigo.md): a 0064 e a fila, perto de 450; a
> reexecução e o trabalhador, 350; as rotas e as três telas, 550; o resto é folga. Se o plano da
> fatia estimar mais, ele para e pede o corte antes do código.

A Curadoria abre `/sanitizacao` e vê, por versão da lista de passos, quantos itens de lotes aceitos
estão nela. Com uma versão nova, ela confirma a reexecução numa página própria, como no
esvaziamento (`EmptyingScreensEndpoint`, linhas 26 e 27). A confirmação só enfileira os itens e
leva à tela de acompanhamento; nenhum item é tratado na requisição. Um trabalhador em segundo plano
esvazia a fila, item a item. O acompanhamento mostra o pedido na fila, em andamento, parado ou
concluído, com idênticos, substituídos e recusados, cada recusa com o motivo. Parado é o pedido
aberto sem item concluído no prazo de uma posse, contado do último concluído ou, sem nenhum, do
pedido. Sem script, a tela se atualiza por um link que recarrega a página. O protótipo é
`reexecucao.html`, em [protótipos](prototipos/index.html).

**Versão que muda com o pedido aberto.** O trabalhador só toma item de pedido cuja alvo é a versão
dele, pois os passos de outra versão não estão no processo. Depois de um deploy que muda a lista, o
pedido aberto da versão anterior está superado: o acompanhamento diz isso e leva à situação, e o
pedido novo conclui os itens que faltam nele como superados, sem tocar o texto, e os enfileira pela
vigente.

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | RerunOfTheSameVersionGivesTheIdenticalText (#97 CA-1, propriedade) |
| `Core.Persistence.Content.Tests` | RerunWithTheSameVersionIsReportedIdenticalAndNotRewritten (#97 CA-1); RerunOfAnEmptiedItemIsRefusedWithTheReason (#97 CA-2); RerunOfAnItemWithLostKeyIsRefusedWithTheReason (#97 CA-2); NewVersionReplacesTheTextAndRecordsTheVersion (#97 CA-3); InterruptedRerunLeavesNoItemWithTextOfOneVersionAndRecordOfAnother (#97 CA-4); RerunEventRecordsWhatWithWhichVersionAndResult (#97 CA-5, 0064); ResanitizingWithoutTheEventIsRefused (0064); AnItemAskedTwiceIsQueuedOnce (0064); TwoWorkersTakeEachQueuedItemOnce (0064); ExpiredHoldIsTakenAgain (0064); ItemThatKeepsFailingIsRefusedAtTheLastTry (0064); SerializationConflictReturnsTheItemWithoutSpendingATry (0064); NewRequestClosesTheOpenRerunOfAnotherTargetAsSuperseded (0064) |
| `Core.Host.Tests` | RerunScreenShowsItemsByVersion; NothingToRerunSaysSoAndOffersNoButton; RerunIsConfirmedOnItsOwnPage; ConfirmationQueuesWithoutTreatingAnyItem; ConfirmedRerunRedirectsToTheProgress; SituationWithAnOpenRerunLinksToItAndOffersNoButton; SecondConfirmationLeadsToTheOpenRerun; ProgressShowsQueuedRunningStoppedAndDone; RerunWithNoItemConcludedWithinTheHoldIsShownStopped; OpenRerunWhoseTargetIsNoLongerCurrentIsShownSupersededAndNotTaken; RerunResumesAfterTheProcessRestarts; DatabaseLostWhileRerunningKeepsTheItemsAlreadyTreated; ReviewAfterRerunShowsTheNewVersion; RerunWithoutCurationAnswersNotFound |
| `Core.Host.Tests.Browser` | RerunIsConfirmedByKeyboardAndPassesTheAccessibilityCheck; ProgressIsReadByKeyboardAndPassesTheAccessibilityCheck |

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | `0064` em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Reexecução | SanitizationRerun (novo), um item por vez, sobre a lista de passos da fatia B | [002](../../padroes/002-servico-de-dominio.md) |
| Fila | um armazenamento novo, ao lado de `ContentWriter`: enfileirar, tomar posse, concluir | [001](../../padroes/001-armazenamento.md) |
| Trabalhador | um serviço em segundo plano, como `SealSendingService`, registrado em `CompositionRoot` | [002](../../padroes/002-servico-de-dominio.md) |
| Rotas | um endpoint novo, como `EmptyingScreensEndpoint`, e `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Telas | a reexecução e o acompanhamento, com os estados do protótipo | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** item repetido, dois pedidos, dois trabalhadores, fila travada, item que sempre falha,
  conflito de serialização, versão que muda, queda e reinício, banco caído e item
  esvaziado, nas linhas da fatia E em [riscos](riscos.md).
- **Raia:** a reexecução, a fila, o trabalhador, as rotas e as telas novas. Lê a lista de passos e
  não a altera; não toca a submissão.
- **Depende de:** a fatia B. **Bloqueia:** nada; roda em paralelo com C e D.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
