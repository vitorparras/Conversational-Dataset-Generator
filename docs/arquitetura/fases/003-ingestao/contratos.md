# Contratos entre módulos da fase 3

> Norma pendente. 2026-11-15. Os nomes novos valem como proposta até o plano da fatia.

A rota de tela chama o mesmo serviço que a rota de JSON já chama, e nunca o armazenamento direto
([rota](../../padroes/003-rota.md)). Quem alcança cada rota é `PersonFunctions.Curation`.

## Fatia A

**Tela → serviço da submissão.** `SubmissionService.SubmitAsync` (linha 50) e `GateAsync`
(linha 42) já existem, e `SubmissionsEndpoint` (linha 76) já os chama com RequestAborted. A rota
de tela lê o formulário em `SubmissionRequest` e traduz o `SubmissionOutcome`:

- aceito: 303 para `/lotes/{id}`;
- recusado: a tela de novo, com o código fixo;
- nome descartado: 303 para o lote, que mostra o aviso;
- porta fechada: a tela com 503.

**Serviço → armazenamento (#775).** `SubmissionStoring.StoreAsync` (linha 76) também trata o
OperationCanceledException, que hoje escapa. `CleanUpAsync` passa a receber o motivo e rejeita
com submission_cancelled num token próprio, porque o da requisição já foi cancelado. Um lote
cancelado não fica aberto.

**Tela → leitura do lote.** `BatchRegister.ReadAsync` (linha 72) e `PreviousAsync` (linha 97)
seguem como estão. Entra `BatchRegister.CountsAsync(domains, batchId)`, que chama a função da 0057
e devolve BatchCounts: forma, destino e replicado, só números. O destino sai do banco como
`OccurrenceDestination` e chega à tela pelos códigos de `BatchDetailTexts` (linha 14), num mapa
único na rota.

## Fatia B

**Host → revisão.** `BatchReviews` entra na injeção do host com o tamanho de
`AdmissionConfiguration.ReviewSampleSize` (linha 47), como o construtor já pede (linha 28).

**Tela → revisão.** A tela da amostra já existe e revisa item a item, na ordem
(SampleReviewScreen.razor, linhas 44 a 66). Além do que já existe (`DrawAsync`, linha 43;
`ReadAsync`, linha 70; `ReviewAsync`, linha 55), entram três operações:

- MarkAsync: marca o próximo item como revisado, e só ele (0058);
- ReadTextAsync: grava o evento de leitura e lê o texto pela 0059, na mesma transação, sem log;
- ApproveAsync: revisa sem achado e aceita o lote numa transação só, como a 0054 permite.

`BatchReviewing` passa a levar a posição do achado. A rota traduz cada `BatchReviewOutcome` num
`switch` completo; `AlreadyReviewed` e `AlreadySampled` mostram quem chegou antes, nunca 500.

**Reprovação → remoção.** A reprovação por achado remove as marcas, as ocorrências, os mapas e os
itens, como `BatchStateChange` já faz (linhas 38 a 41), na mesma transação.

## Rotas

O endereço da revisão é o `Action` das duas telas (SampleReviewScreen.razor, linha 122).

| Rota | Verbo | Fatia |
|---|---|---|
| `/lotes/novo` e `/lotes/{id}` | GET, POST; GET | A |
| `/lotes/{id}/amostra` | POST | B |
| `/lotes/{id}/revisao` | GET | B |
| `/lotes/{id}/revisao/itens/{n}/revisado` e `/lotes/{id}/revisao/aprovar` | POST | B |
| `/lotes/{id}/revisao/itens/{n}/achado` | GET, POST | B |
