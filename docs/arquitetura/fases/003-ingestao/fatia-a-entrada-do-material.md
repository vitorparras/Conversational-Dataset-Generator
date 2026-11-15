# Fatia A — entrada do material até o lote

> Norma pendente. 2026-11-15. Fecha #607, #609 e #775. Teto da fatia: 900 linhas de produção.

A curadoria abre `/lotes/novo`, envia o arquivo e cai no lote aberto, que mostra as contagens, a
procedência e a perda declarada. Os protótipos são `submissao.html` e `lote.html`, em
[protótipos](prototipos/index.html).

## Critérios de aceite

`Core.Persistence.Ingestion.Tests`, com banco:

- DeclaredFormIsKeptOnOpeningAndCannotChange (0055)
- DestinationCountsCannotChangeAndEmptyingZeroesThem (0056)
- BatchDetailCountsOfAnOpenBatchReturnOnlyNumbers (0057)
- CancelledWhileWritingLeavesTheBatchRejectedWithNoItems (#775)
- ResubmittingAfterCancellationOpensANewBatch (#775)

`Core.Host.Tests`, rota e tela:

- SubmissionWithoutOriginReturnsTheErrorOnTheFieldAndWritesNothing (#607 CA-1)
- SubmissionWithoutConditionReturnsTheErrorOnTheFieldAndWritesNothing (#607 CA-1)
- OversizeFileIsRefusedByCodeWithoutEchoAndRecorded (#607 CA-2)
- FileOfAnotherFormatIsRefusedByCodeWithoutEchoAndRecorded (#607 CA-2)
- PathNameIsDiscardedAndNeverEchoed (#607 CA-2)
- ClosedGateSaysSoAndOffersNoUpload (#607 CA-3)
- AcceptedSubmissionLeadsToTheOpenBatch (#607 CA-4)
- SameFileTwiceLeadsTheSecondToTheFirstBatch
- DatabaseLostWhileWritingAnswersThatNothingWasKept
- BatchDetailCountsByFormDestinationAndReplica (#609 CA-1)
- ResubmissionShowsTheBatchThatRecognizedIt (#609 CA-2)
- AccommodatedFormShowsTheDeclaredLoss (#609 CA-3)
- BatchDetailNeverShowsMaterialText (#609 CA-4)
- BatchOfAnotherDomainAnswersNotFound

`Core.Host.Tests.Browser`:

- SubmissionLeadsToTheBatchByKeyboardAndPassesTheAccessibilityCheck

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migrações | `0055` a `0057` em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Contagens | `BatchRegister`, BatchCounts (novo) | [001](../../padroes/001-armazenamento.md) |
| Cancelamento | `SubmissionStoring` | [002](../../padroes/002-servico-de-dominio.md) |
| Rotas | `BatchScreensEndpoint`, `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Telas | as peças da submissão e do lote, já no main (#732, #755), ligadas às rotas | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** concorrência no envio, cancelamento no meio, falha do banco, nome com caminho e
  texto no detalhe, nas linhas da fatia A em [riscos](riscos.md).
- **Raia:** a submissão (`core/src/Core.Host/Submissions`), o registro do lote e as duas telas.
  Não toca `BatchReviews` nem as telas da amostra.
- **Depende de:** nada aberto (#801 já está no main). **Bloqueia:** a fatia B.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
