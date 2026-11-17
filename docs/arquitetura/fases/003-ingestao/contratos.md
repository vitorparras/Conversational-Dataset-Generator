# Contratos entre módulos da fase 3

> Norma pendente. 2026-11-15. Os nomes novos valem como proposta até o plano da fatia.

A rota de tela chama o serviço, nunca o armazenamento ([rota](../../padroes/003-rota.md)). Toda
rota nova declara `PersonFunctions.Curation` em `ScreenAccess`; cada POST novo tem um teste de
pessoa sem Curadoria, recusada sem gravar nada.

## Fatia A

**Tela → submissão.** A rota usa o portão de `SubmissionsEndpoint` (linhas 34 a 42, que chamam
`IntakeDoor.StatusAsync`) e `SubmissionService.SubmitAsync` (linha 50) com RequestAborted. Aceito
ou com nome descartado, 303 para `/lotes/{id}`; recusado, a tela com o código fixo; porta
fechada, a tela com 503.

**Submissão → armazenamento (#775).** `SubmissionStoring.StoreAsync` (linha 57) passa a tratar
também o OperationCanceledException, que hoje escapa. O fechamento de `KeepAsync` (linha 172)
rejeita com submission_cancelled, num token próprio da limpeza, porque o da requisição já caiu.

**Repetição.** A submissão só reconhece a cópia de um lote aceito, por `SubmissionJudgement`
(linhas 29 a 33) e pelo índice parcial da 0051. Duas cópias abertas entram as duas, e quem decide
é a aprovação (fatia B). A fatia A apagou o que nunca rodava: a repetição em Keeping,
no armazenamento, no serviço e no julgamento da submissão.

**Tela → leitura do lote.** `BatchRegister.ReadAsync` (linha 72) segue, e uma leitura nova chama
a 0057: só números de forma, destino e replicado, com os códigos de `BatchDetailTexts` (linha 14).

## Fatia B

**Decisão separada da gravação** ([serviço de domínio](../../padroes/002-servico-de-dominio.md)):
- O progresso da amostra, um tipo puro novo em `Core.Ingestion`, decide o próximo item, se o
  achado cabe e se o lote pode ser aprovado. As recusas de estado de `BatchReviews` viram uma
  função pura em `Core.Ingestion.Contracts`, chamada pelo banco e pela mesa da revisão (`BatchReviews.Refusal`), testada sem banco ([0055](../../../decisoes/0055-recusas-da-revisao-em-contrato-da-ingestao.md)).
- Um serviço da revisão em `Core.Host`, como o `SubmissionService`, lê o lote com
  `BatchReviews.ReadAsync`, pede a decisão e chama a gravação.
- `BatchReviews` fica só com gravações condicionais: cada UPDATE só pega a linha se a posição
  revisada for a esperada, com o SQL numa classe de `Statements`. Sem linha pega, a gravação
  devolve o fato e o serviço relê. Cada uma copia o laço de nova tentativa do exemplar do padrão
  001 no `Conflict` (`CustodyTransaction`, linha 55): quem perde a disputa vê
  `AlreadyReviewed` ou o item já marcado, nunca `NotRecorded`.

**Aprovação de uma cópia.** A aprovação revisa e aceita na mesma transação. Se o aceite esbarrar no
índice da 0051, a gravação captura a UniqueViolation como `IngestionBatches` (linhas 206 a 208),
tudo volta, e `BatchReviewOutcome` ganha o valor novo "já recebido". Numa transação nova, o
serviço rejeita o lote com already_received (`IngestionBatches.RejectAsync`, linha 86) e acha o
primeiro por `BatchRegister.PreviousAsync`; a tela leva ao lote, que aponta para o primeiro.

**Reprovação → remoção.** É mudança: hoje a reprovação remove só mapas e itens (`BatchReviews`,
linhas 210 e 211). Ela passa a remover também marcas e ocorrências, como `BatchStateChange`
(linhas 39 a 42), na mesma transação.

**Leitura por GET.** Abrir a revisão grava um evento por item mostrado, pois a #610 CA-5 pede o
registro de cada leitura; o evento não muda o lote nem o item, e o padrão 003 proíbe só isso.

| Rota | Verbo | Fatia |
|---|---|---|
| `/lotes/novo` e `/lotes/{id}` | GET, POST; GET | A |
| `/lotes/{id}/amostra`, `/lotes/{id}/revisao/aprovar`, `/lotes/{id}/revisao/itens/{n}/revisado` | POST | B |
| `/lotes/{id}/revisao` | GET | B |
| `/lotes/{id}/revisao/itens/{n}/achado` | GET, POST | B |
