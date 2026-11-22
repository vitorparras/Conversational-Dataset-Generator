# Riscos da fase 4

> Norma pendente. 2026-11-15. Cada risco tem o teste que o ataca, com o nome da fatia.

A sanitização é domínio puro: o mesmo bruto e a mesma versão dão o mesmo texto, em qualquer
processo. Os riscos de concorrência e de falha ficam na gravação da submissão, transação
serializável sem nova tentativa automática, e na fila da reexecução, em que cada item é uma
transação serializável e a posse vencida devolve o item à fila.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | Duas pessoas pedem a reexecução ao mesmo tempo, ou o mesmo pedido chega duas vezes | O índice único parcial da 0064 deixa o item em um só pedido aberto; o segundo pedido não o enfileira de novo | E: AnItemAskedTwiceIsQueuedOnce |
| Concorrência | A Curadoria confirma de novo com os itens ainda na fila | Sem item novo a enfileirar, não grava pedido e leva ao acompanhamento do pedido aberto; com item novo (lote aceito depois do pedido), enfileira-o no pedido aberto, com o evento de quem confirmou, e leva a ele | E: SecondConfirmationLeadsToTheOpenRerun, RequestWithANewItemQueuesItInTheOpenRerun |
| Concorrência | Dois trabalhadores, ou duas instâncias, tomam o mesmo item | A posse é tomada com FOR UPDATE SKIP LOCKED e prazo; o UPDATE só pega item de versão diferente da alvo, e a conclusão na fila vai na mesma transação, então o item já tratado não é tomado de novo | E: TwoWorkersTakeEachQueuedItemOnce |
| Concorrência | A transação do item perde para outra e o banco a cancela (40001) | O item volta à fila sem gastar tentativa e é tomado de novo | E: SerializationConflictReturnsTheItemWithoutSpendingATry |
| Versão que muda | Um deploy muda a lista de passos com um pedido aberto | O trabalhador não toma item de outra alvo; o acompanhamento mostra o pedido superado, e o pedido novo conclui os itens que faltam nele como superados | E: OpenRerunWhoseTargetIsNoLongerCurrentIsShownSupersededAndNotTaken; NewRequestClosesTheOpenRerunOfAnotherTargetAsSuperseded |
| Concorrência | A reexecução troca o sanitizado durante a revisão da amostra | A revisão lê o texto na hora, e a tela da amostra mostra a versão do texto que exibe | E: ReviewAfterRerunShowsTheNewVersion |
| Concorrência | Dois lotes gravam pela primeira vez a mesma versão | A 0060 grava a versão com ON CONFLICT DO NOTHING; os dois lotes apontam para a mesma linha | B: SameVersionFromTwoBatchesIsStoredOnce |
| Cancelamento no meio | A requisição cai durante a análise, antes do primeiro item | Nada foi gravado; o lote é rejeitado com submission_cancelled, como na fase 3 | A: CancelledWhileAnalyzingLeavesTheBatchRejectedWithNoItems |
| Cancelamento no meio | A requisição cai entre dois itens sanitizados | Cada item nasce com o sanitizado no mesmo INSERT; o lote é rejeitado e a limpeza da fase 3 tira os itens | B: CancelledWhileSanitizingLeavesNoItemWithoutSanitized |
| Cancelamento no meio | A reexecução cai no meio do conjunto | Uma transação por item: texto, versão, evento e conclusão na fila vão juntos ou nada vai | E: InterruptedRerunLeavesNoItemWithTextOfOneVersionAndRecordOfAnother (#97 CA-4) |
| Disponibilidade | A reexecução de um acervo grande prende a requisição | A confirmação só enfileira; o trabalhador trata os itens fora da requisição | E: ConfirmationQueuesWithoutTreatingAnyItem |
| Fila travada | O trabalhador para com a posse de um item e a fila não anda | A posse tem prazo; vencida, o item volta a ser tomado, e a tela mostra o pedido parado quando nenhum item é concluído no prazo de uma posse, contado do último concluído ou, sem nenhum, do pedido | E: ExpiredHoldIsTakenAgain; RerunWithNoItemConcludedWithinTheHoldIsShownStopped |
| Fila travada | Um item falha toda vez e prende o pedido | Conta tentativa a posse que vence e a que falha, menos o conflito de serialização; a última conclui o item como recusado, com o motivo, e o resto segue | E: ItemThatKeepsFailingIsRefusedAtTheLastTry |
| Queda do processo | O processo cai ou reinicia com a fila pela metade | A fila está no banco; na subida o trabalhador retoma os itens na fila e os de posse vencida, e os concluídos não voltam | E: RerunResumesAfterTheProcessRestarts |
| Falha de rede | O serviço de linguagem não responde | Nenhuma análise, nenhum item: o lote é rejeitado com analysis_unavailable e a tela do lote diz por quê | A: LanguageServiceDownRejectsTheBatchAsAnalysisUnavailable |
| Falha de rede | O banco some durante a reexecução | O item da vez não muda e volta à fila quando a posse vence; os já tratados ficam; a tela diz que o pedido está parado | E: DatabaseLostWhileRerunningKeepsTheItemsAlreadyTreated |
| Dado pessoal | Uma exceção num passo leva o texto ao log | O registro traz lote, posição do item, passo e tipo da exceção, nunca o texto | B: StepFailureLogCarriesNoMaterialText (#93 CA-4) |
| Dado pessoal | O relato do que foi removido mostra o removido | A tela do lote mostra só as contagens por passo; o rastro com as posições é cifrado e não vai a tela alguma | B: ReportAndLogsOfASeededBatchCarryNoSeededValue (#93 CA-3) |
| Dado pessoal | O marcador revela o tamanho ou um pedaço do removido | O marcador traz só o tipo, pela tabela da fatia B dos [contratos](contratos.md); nunca um caractere nem o tamanho do original | B: RemovedValueLeavesATypedMarkerWithNoCharacterOfIt (#90 CA-1) |
| Dado pessoal | Uma cópia do banco expõe o sanitizado | O sanitizado e o rastro vão selados; a varredura de cópia não acha o texto semeado | B: DatabaseCopyHasNoReadableSanitizedText (#98 CA-2) |
| Dado pessoal | O sanitizado vai para a coluna em claro | A coluna do pseudonimizado fica nula em toda a fase; um teste a consulta ao final | B: ClearTextColumnHoldsNoMaterialAtTheEndOfThePhase (#98 CA-4) |
| Dado pessoal | A reexecução trabalha sobre o sanitizado | Ela decifra o bruto; item esvaziado, ou de chave perdida, é recusado com o motivo | E: RerunOfAnEmptiedItemIsRefusedWithTheReason (#97 CA-2) |

## O que fica de fora

- **Anotação desatualizada.** Não há anotação nesta fase; marcar a anotação afetada pela
  reexecução é da fase de anotação (#97, "O que esta issue não faz").
- **Pseudonimização.** O passo dos destinos deixa o pseudonimizado no texto e traduz a posição;
  trocar o nome é da fase 5.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).
