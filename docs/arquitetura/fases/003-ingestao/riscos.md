# Riscos da fase 3

> Norma pendente. 2026-11-15. Cada risco tem o teste que o ataca, com o nome da fatia.

As transações são serializáveis e sem nova tentativa automática, além do que os lotes já fazem
(`SubmissionStoring.RejectionRounds`, linha 27). A primeira escrita que confirma ganha.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | Duas pessoas enviam o mesmo arquivo | O banco decide; o segundo lote é rejeitado como already_received e a tela leva ao primeiro | A: SameFileTwiceLeadsTheSecondToTheFirstBatch |
| Concorrência | Duas pessoas sorteiam a amostra do mesmo lote | O segundo recebe `AlreadySampled` e vê a amostra que já existe | B: DrawingTwiceShowsTheSampleAlreadyDrawn |
| Concorrência | Uma aprova e outra reprova o mesmo lote | A primeira revisão vale; a outra vê "revisado por outra pessoa", sem 500 | B: ReviewLostToAnotherShowsWhoReviewedFirst |
| Concorrência | Duas pessoas marcam o mesmo item | A 0058 só aceita a posição seguinte; a segunda vê o item já marcado | B: MarkingTheSameItemTwiceKeepsOneMark |
| Cancelamento no meio | A requisição cai durante a gravação dos itens (#775) | O lote é rejeitado com submission_cancelled num token próprio, sem itens | A: CancelledWhileWritingLeavesTheBatchRejectedWithNoItems |
| Cancelamento no meio | A aprovação cai entre revisar e aceitar | As duas coisas vão numa transação; cai junto e o lote segue aberto | B: ApprovalCancelledLeavesTheBatchOpenAndUnreviewed |
| Cancelamento no meio | A reprovação cai no meio da remoção | A remoção está na mesma transação da reprovação | B: RejectionCancelledLeavesEveryItemInPlace |
| Falha de rede | O banco some durante o envio | NpgsqlException vira write_failed, e a tela diz que nada foi guardado | A: DatabaseLostWhileWritingAnswersThatNothingWasKept |
| Falha de rede | O banco some depois do POST da aprovação | Não há meio-termo: a tela do lote mostra o estado real ao recarregar | B: ApprovalWithDatabaseLostAnswersUnavailable |
| Dado pessoal | O nome do arquivo traz caminho ou nome de pessoa | O nome é descartado e registrado por código; a tela não o ecoa | A: PathNameIsDiscardedAndNeverEchoed |
| Dado pessoal | O detalhe do lote mostra texto | Só números saem da função da 0057 | A: BatchDetailNeverShowsMaterialText |
| Dado pessoal | O texto da amostra vai ao log | A leitura grava o evento sem conteúdo; o log só leva lote e item | B: SampleReadIsRecordedWithoutContent |
| Dado pessoal | O texto de um item fora da amostra é lido | A função da 0059 recusa um item fora de review_sample | B: TextOutsideTheSampleIsRefused |
| Dado pessoal | A reprovação por achado deixa resto | Marcas, ocorrências, mapas e itens saem juntos | B: RejectionForFindingLeavesNoTraceOfTheItems |

## O que fica de fora

- **Reenvio depois de cancelado.** O lote rejeitado por cancelamento não conta como recebido: o
  mesmo arquivo pode vir de novo. A fatia A prova isso em
  ResubmittingAfterCancellationOpensANewBatch.
- **Arquivo grande.** A recusa por tamanho já existe e não muda; a fatia A só a liga à tela.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).
