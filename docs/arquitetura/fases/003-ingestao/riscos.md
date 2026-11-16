# Riscos da fase 3

> Norma pendente. 2026-11-15. Cada risco tem o teste que o ataca, com o nome da fatia.

As transações são serializáveis, e quem perde uma disputa cai no `Conflict`
(`CustodyTransaction`, linha 55), que `BatchReviews` hoje devolve como `NotRecorded` (linhas
116 a 119). As gravações novas copiam o laço de nova tentativa do exemplar do
[armazenamento](../../padroes/001-armazenamento.md). Na nova tentativa, a gravação condicional não
pega linha, e o serviço relê e mostra quem chegou antes.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | O mesmo arquivo de um lote já aceito chega de novo | A submissão o reconhece pelo lote aceito e o rejeita como already_received | A: SameFileAsAnAcceptedBatchIsRejectedAsAlreadyReceived |
| Concorrência | Duas cópias do mesmo arquivo ficam abertas ao mesmo tempo | Entram as duas; o aceite da segunda esbarra no índice da 0051, e o serviço a rejeita como already_received e leva ao primeiro lote | B: ApprovingACopyOfAnAcceptedFileRejectsItAsAlreadyReceived, ApprovingACopyLeadsToTheFirstBatch |
| Concorrência | Duas pessoas sorteiam a amostra do mesmo lote | A segunda recebe `AlreadySampled` e vê a amostra que já existe | B: DrawingTwiceShowsTheSampleAlreadyDrawn |
| Concorrência | Uma pessoa aprova e outra reprova o mesmo lote | Nova tentativa, gravação condicional sem linha e releitura: a outra vê "revisado por outra pessoa", sem 500 | B: ReviewLostToAnotherShowsWhoReviewedFirst |
| Concorrência | Duas pessoas marcam o mesmo item | A 0058 só aceita a posição seguinte; a segunda vê o item já marcado | B: MarkingTheSameItemTwiceKeepsOneMark |
| Cancelamento no meio | A requisição cai durante a gravação dos itens (#775) | O fechamento de `KeepAsync` rejeita com submission_cancelled num token próprio | A: CancelledWhileWritingRejectsTheBatchAsSubmissionCancelled, CancelledWhileWritingLeavesNoItems |
| Cancelamento no meio | O mesmo arquivo volta depois do cancelamento | O lote cancelado não conta como recebido, e o envio abre um lote novo | A: ResubmittingAfterSubmissionCancelledOpensANewBatch |
| Cancelamento no meio | A aprovação cai entre revisar e aceitar | Os dois UPDATEs vão numa transação: caem juntos e o lote segue aberto | B: ApprovalCancelledLeavesTheBatchOpenAndUnreviewed |
| Cancelamento no meio | A reprovação cai no meio da remoção | A remoção está na mesma transação da reprovação | B: RejectionCancelledLeavesEveryItemInPlace |
| Falha de rede | O banco some durante o envio | NpgsqlException vira write_failed, e a tela diz que nada foi guardado | A: DatabaseLostWhileWritingAnswersThatNothingWasKept |
| Falha de rede | O banco some depois do POST da aprovação | A tela diz que o serviço está indisponível; ao recarregar, o lote aparece no estado real | B: ApprovalWithDatabaseLostAnswersUnavailable, ReloadAfterALostApprovalShowsTheBatchStillOpen |
| Entrada | O arquivo chega vazio | Hoje não há recusa própria (`SubmissionRefusal`); se o leitor não o recusar, a fatia A soma uma com código fixo | A: EmptyFileIsRefusedAndLeavesNoOpenBatch |
| Acesso | Alguém sem Curadoria posta direto numa rota nova | `ScreenAccess` recusa antes do serviço, e nada é gravado | A: SubmissionWithoutCurationIsRefusedAndWritesNothing; B: DrawingWithoutCurationIsRefused e os outros três POST |
| Dado pessoal | O nome do arquivo traz caminho ou nome de pessoa | O nome é descartado e registrado só por código; a tela não o ecoa | A: PathNameIsNeverEchoed, PathNameIsRecordedOnlyByItsCode |
| Dado pessoal | O detalhe do lote mostra texto | Só números saem da função da 0057 | A: BatchDetailNeverShowsMaterialText |
| Dado pessoal | O texto da amostra vai ao log | A leitura grava o evento sem conteúdo; o log só leva lote e item | B: SampleReadIsRecordedWithoutContent, SampleReadWritesNoTextToTheLog |
| Dado pessoal | O texto de um item fora da amostra é lido | A função da 0059 recusa o item fora de review_sample | B: TextOutsideTheSampleIsRefused |
| Dado pessoal | A reprovação por achado deixa resto | Marcas, ocorrências, mapas e itens saem juntos | B: RejectionForFindingRemovesMarksAndOccurrences |

## O que fica de fora

- **Arquivo grande.** A recusa por tamanho já existe (too_large) e não muda; a fatia A só a liga
  à tela.
- **Tamanho da amostra por domínio.** Nesta fase, o tamanho é o da instalação
  (`AdmissionConfiguration.ReviewSampleSize`); sem ele declarado, a tela não oferece o sorteio.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).
