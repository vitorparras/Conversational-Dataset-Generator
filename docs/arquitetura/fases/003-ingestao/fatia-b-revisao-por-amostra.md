# Fatia B — revisão por amostra até o lote aceito ou reprovado

> Norma pendente. 2026-11-15. Fecha #610. Teto da fatia: 800 linhas de produção.

Da tela do lote aberto, a curadoria sorteia a amostra, revisa cada item na ordem e aprova o lote
ou o reprova por achado. Os protótipos são `revisao.html` e `reprovacao.html`, em
[protótipos](prototipos/index.html). Os nomes abaixo são os testes a escrever; nenhum existe ainda.

## Critérios de aceite

`Core.Persistence.Ingestion.Tests`, com banco:

- ReviewProgressOnlyGrowsByOneAndFindingNeedsARejection (0058)
- SampleTextOfAnOpenSampledBatchIsReadOnlyAfterTheReadEvent (0059)
- TextOutsideTheSampleIsRefused (0059)
- ApprovalCancelledLeavesTheBatchOpenAndUnreviewed
- RejectionCancelledLeavesEveryItemInPlace
- RejectionForFindingLeavesNoTraceOfTheItems

`Core.Host.Tests`, rota e tela:

- SampleHasTheDeclaredSizeAndOffersNoChoice (#610 CA-1)
- FindingAsksForRejectionOnItsOwnScreen (#610 CA-2)
- RejectionRecordsWhoWhatAndWhen (#610 CA-2)
- ApprovalRecordsWhichSampleWhoAndWhen (#610 CA-3)
- NoApprovalWithoutAReviewedSample (#610 CA-4)
- SampleReadIsRecordedWithoutContent (#610 CA-5)
- DrawingTwiceShowsTheSampleAlreadyDrawn
- ReviewLostToAnotherShowsWhoReviewedFirst
- MarkingTheSameItemTwiceKeepsOneMark
- ItemAfterTheNextCannotBeMarked (#610 CA-1)
- ApprovalWithDatabaseLostAnswersUnavailable
- GoingBackFromRejectionLeavesTheBatchOpen

`Core.Host.Tests.Browser`:

- SampleReviewByKeyboardReachesEachItemAndPassesTheAccessibilityCheck

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migrações | `0058` e `0059` em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Revisão | `BatchReviews` (MarkAsync, ReadTextAsync e ApproveAsync novos) | [002](../../padroes/002-servico-de-dominio.md) |
| Injeção | `AdmissionConfiguration`, o registro do host | [002](../../padroes/002-servico-de-dominio.md) |
| Rotas | `BatchScreensEndpoint`, `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Telas | as peças da amostra e da reprovação, já no main (#793), ligadas às rotas | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** sorteio duplo, revisão disputada, cancelamento entre revisar e aceitar, banco perdido,
  texto no log e item fora da amostra, nas linhas da fatia B em [riscos](riscos.md).
- **Raia:** `BatchReviews`, a 0058, a 0059 e as telas da revisão e da reprovação. Não toca a
  submissão.
- **Depende de:** a fatia A, porque a amostra parte da tela do lote em `/lotes/{id}`.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
