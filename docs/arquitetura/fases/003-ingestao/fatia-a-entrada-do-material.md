# Fatia A — entrada do material até o lote

> Norma pendente. 2026-11-15. Fecha #607, #609 e #775. Teto da fatia: 900 linhas de produção.

A curadoria abre `/lotes/novo`, envia o arquivo e cai no lote aberto, que mostra as contagens, a
procedência e a perda declarada. Os protótipos são `submissao.html` e `lote.html`, em
[protótipos](prototipos/index.html). Os nomes abaixo são os testes a escrever; nenhum existe ainda.
Cada um prova um comportamento; onde a linha traz vários, são testes separados.

## Critérios de aceite

| Projeto | Origem | Testes |
|---|---|---|
| `Core.Persistence.Ingestion.Tests` | 0055 | DeclaredFormIsKeptOnOpening, DeclaredFormCannotChange |
| `Core.Persistence.Ingestion.Tests` | 0056 | DestinationCountsCannotChange, EmptyingZeroesTheDestinationCounts |
| `Core.Persistence.Ingestion.Tests` | 0057 | BatchDetailCountsOfAnOpenBatchReturnOnlyNumbers |
| `Core.Host.Tests`, com banco | #775 | CancelledWhileWritingRejectsTheBatchAsSubmissionCancelled, CancelledWhileWritingLeavesNoItems, ResubmittingAfterSubmissionCancelledOpensANewBatch |
| `Core.Host.Tests` | #607 CA-1 | SubmissionWithoutOriginReturnsTheErrorOnTheFieldAndWritesNothing, SubmissionWithoutConditionReturnsTheErrorOnTheFieldAndWritesNothing |
| `Core.Host.Tests` | #607 CA-2 | FileRefusedByTheGateShowsItsCode, FileRefusedByTheGateIsNotEchoed, FileRefusedByTheGateIsRecordedAsARejectedBatch (teorias com o arquivo grande e o de outro formato) |
| `Core.Host.Tests` | #607 CA-2 | PathNameIsNeverEchoed, PathNameIsRecordedOnlyByItsCode, PathNameShowsTheWarningOnTheBatch, SubmissionWithAPathNameIsAccepted |
| `Core.Host.Tests` | #607 CA-2 | EmptyFileIsRefusedAndLeavesNoOpenBatch |
| `Core.Host.Tests` | #607 CA-3 | ClosedGateSaysWhyItIsClosed, ClosedGateOffersNoUpload |
| `Core.Host.Tests` | #607 CA-4 | AcceptedSubmissionLeadsToTheOpenBatch |
| `Core.Host.Tests` | #609 CA-1 a CA-4 | BatchDetailCountsByFormDestinationAndReplica, ResubmissionShowsTheBatchThatRecognizedIt, AccommodatedFormShowsTheDeclaredLoss, BatchDetailNeverShowsMaterialText |
| `Core.Host.Tests` | riscos | SameFileAsAnAcceptedBatchIsRejectedAsAlreadyReceived, DatabaseLostWhileWritingAnswersThatNothingWasKept, BatchOfAnotherDomainAnswersNotFound |
| `Core.Host.Tests` | acesso | SubmissionWithoutCurationIsRefusedAndWritesNothing |
| `Core.Host.Tests.Browser` | telas | SubmissionReachesTheBatchByKeyboard, SubmissionScreenPassesTheAccessibilityCheck, BatchScreenPassesTheAccessibilityCheck |

Os testes do #775 ficam em `Core.Host.Tests` porque `SubmissionStoring` é `internal` em
`Core.Host` e só aparece para os dois projetos de teste do host.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migrações | 0055 a 0057 em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Contagens | `BatchRegister` e uma leitura nova das contagens | [001](../../padroes/001-armazenamento.md) |
| Cancelamento | `SubmissionStoring` | [002](../../padroes/002-servico-de-dominio.md) |
| Repetição | apagar o caminho morto em `SubmissionStoring`, `SubmissionService` e `SubmissionJudgement` | [002](../../padroes/002-servico-de-dominio.md) |
| Arquivo vazio | `SubmissionRefusal`, só se o leitor não o recusar hoje | [002](../../padroes/002-servico-de-dominio.md) |
| Rotas | `BatchScreensEndpoint`, `ScreenPaths`, `ScreenAccess` | [003](../../padroes/003-rota.md) |
| Telas | as peças da submissão e do lote, já no main (#732, #755), ligadas às rotas | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** a cópia de lote aceito, o cancelamento no meio e o reenvio, o banco perdido, o arquivo
  vazio, o acesso sem Curadoria, o nome com caminho e o texto no detalhe, nas linhas da fatia A em
  [riscos](riscos.md).
- **Raia:** a submissão (`core/src/Core.Host/Submissions`), o registro do lote e as duas telas.
  Não toca `BatchReviews` nem as telas da amostra.
- **Depende de:** nada aberto (#801 já está no main). **Bloqueia:** a fatia B.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
