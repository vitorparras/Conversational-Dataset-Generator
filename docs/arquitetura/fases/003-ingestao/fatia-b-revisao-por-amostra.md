# Fatia B — revisão por amostra até o lote aceito ou reprovado

> Norma pendente. 2026-11-15. Fecha #610. Teto da fatia: 1.000 linhas de produção.

Da tela do lote aberto, a curadoria sorteia a amostra, revisa cada item na ordem e aprova o lote
ou o reprova por achado. Os protótipos são `revisao.html` e `reprovacao.html`, em
[protótipos](prototipos/index.html). Os nomes abaixo são os testes a escrever; nenhum existe ainda.
Cada um prova um comportamento; onde a linha traz vários, são testes separados.

## Critérios de aceite

| Projeto | Origem | Testes |
|---|---|---|
| `Core.Ingestion.Tests`, sem banco | progresso | NextItemIsTheOneAfterTheLastReviewed, ItemAfterTheNextIsRefused, ApprovalBeforeTheLastItemIsRefused, FindingOnAnItemOtherThanTheNextIsRefused |
| `Core.Persistence.Ingestion.Tests` | 0058 | ReviewProgressGrowsOnlyByOne, ReviewProgressCannotGrowBeforeTheDraw, ReviewProgressCannotGrowAfterTheReview, FindingPositionIsTheItemAfterTheLastReviewed, FindingPositionNeedsARejectionForFinding |
| `Core.Persistence.Ingestion.Tests` | 0059 | SampleTextOfAnOpenSampledBatchIsReadOnlyAfterTheReadEvent, TextOutsideTheSampleIsRefused, SampleTextIsTheSanitizedOneWhenItExists, SampleTextIsTheRawOneWhenNoSanitizedExists |
| `Core.Persistence.Ingestion.Tests` | remoção | ApprovalCancelledLeavesTheBatchOpenAndUnreviewed, RejectionCancelledLeavesEveryItemInPlace, RejectionForFindingLeavesNoTraceOfTheItems, RejectionForFindingRemovesMarksAndOccurrences |
| `Core.Host.Tests` | #610 CA-1 | SampleHasTheDeclaredSize, SampleOffersNoChoiceOfItems, ItemAfterTheNextCannotBeMarked, WithoutADeclaredSampleSizeTheScreenOffersNoDraw |
| `Core.Host.Tests` | #610 CA-2 | FindingAsksForRejectionOnItsOwnScreen, RejectionScreenSaysNoItemWillEnter, RejectionRecordsWhoRejected, RejectionRecordsTheFinding, RejectionRecordsWhen |
| `Core.Host.Tests` | #610 CA-3 e CA-4 | ApprovalRecordsTheReviewedSample, ApprovalRecordsWhoApproved, ApprovalRecordsWhen, NoApprovalWithoutAReviewedSample |
| `Core.Host.Tests` | #610 CA-5 | SampleReadIsRecordedWithoutContent, SampleReadWritesNoTextToTheLog |
| `Core.Host.Tests`, com banco | cópia | ApprovingACopyOfAnAcceptedFileRejectsItAsAlreadyReceived, ApprovingACopyLeadsToTheFirstBatch |
| `Core.Host.Tests`, com banco | disputa | DrawingTwiceShowsTheSampleAlreadyDrawn, ReviewLostToAnotherShowsWhoReviewedFirst, MarkingTheSameItemTwiceKeepsOneMark |
| `Core.Host.Tests` | riscos | ApprovalWithDatabaseLostAnswersUnavailable, ReloadAfterALostApprovalShowsTheBatchStillOpen, GoingBackFromRejectionLeavesTheBatchOpen |
| `Core.Host.Tests` | acesso | DrawingWithoutCurationIsRefused, MarkingWithoutCurationIsRefused, ApprovalWithoutCurationIsRefused, FindingWithoutCurationIsRefused |
| `Core.Host.Tests.Browser` | telas | SampleReviewReachesEachItemByKeyboard, SampleReviewScreenPassesTheAccessibilityCheck, RejectionScreenPassesTheAccessibilityCheck |

Os testes de disputa abrem duas transações de verdade: quem perde passa pelo laço de nova
tentativa e vê o fato, nunca `NotRecorded`.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migrações | 0058 e 0059 em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Progresso | um tipo puro novo em `Core.Ingestion`, com as recusas que saem de `BatchReviews` (linhas 77 a 105) | [002](../../padroes/002-servico-de-dominio.md) |
| Serviço | um serviço da revisão novo em `Core.Host`, como `SubmissionService` | [002](../../padroes/002-servico-de-dominio.md) |
| Gravações | `BatchReviews` só com gravações condicionais, o SQL numa classe de Statements nova e o laço de nova tentativa | [001](../../padroes/001-armazenamento.md) |
| Resultado | `BatchReviewOutcome` ganha o valor "já recebido" | [001](../../padroes/001-armazenamento.md) |
| Tamanho | `AdmissionConfiguration.ReviewSampleSize`, o da instalação, e o registro do host | [002](../../padroes/002-servico-de-dominio.md) |
| Rotas | `BatchScreensEndpoint`, `ScreenPaths`, `ScreenAccess` | [003](../../padroes/003-rota.md) |
| Telas | as peças da amostra e da reprovação, já no main (#793), ligadas às rotas, e o estado sem tamanho declarado | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** sorteio duplo, revisão disputada, item marcado duas vezes, a cópia na aprovação,
  cancelamento entre revisar e aceitar e no meio da remoção, banco perdido e recarregamento, acesso
  sem Curadoria, texto no log e item fora da amostra, nas linhas da fatia B em [riscos](riscos.md).
- **Raia:** `BatchReviews`, o progresso, o serviço da revisão, a 0058, a 0059 e as telas da revisão
  e da reprovação. Não toca a submissão.
- **Depende de:** a fatia A, porque a amostra parte da tela do lote em `/lotes/{id}`.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
