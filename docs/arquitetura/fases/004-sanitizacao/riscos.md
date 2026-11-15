# Riscos da fase 4

> Norma pendente. 2026-11-15. Cada risco tem o teste que o ataca, com o nome da fatia.

A sanitização é domínio puro: o mesmo bruto e a mesma versão dão o mesmo texto, em qualquer
processo. Os riscos de concorrência e de falha ficam na gravação e na reexecução, que são
transações serializáveis, sem nova tentativa automática.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | Duas pessoas pedem a reexecução ao mesmo tempo | O UPDATE só pega item de versão diferente da vigente; o segundo pedido acha o item já tratado e o conta como idêntico | E: TwoRerunsAtOnceTreatEachItemOnce |
| Concorrência | A reexecução troca o sanitizado durante a revisão da amostra | A revisão lê o texto na hora, e a tela da amostra mostra a versão do texto que exibe | E: ReviewAfterRerunShowsTheNewVersion |
| Concorrência | Dois lotes gravam pela primeira vez a mesma versão | A 0060 grava a versão com ON CONFLICT DO NOTHING; os dois lotes apontam para a mesma linha | B: SameVersionFromTwoBatchesIsStoredOnce |
| Cancelamento no meio | A requisição cai durante a análise, antes do primeiro item | Nada foi gravado; o lote é rejeitado com submission_cancelled, como na fase 3 | A: CancelledWhileAnalyzingLeavesTheBatchRejectedWithNoItems |
| Cancelamento no meio | A requisição cai entre dois itens sanitizados | Cada item nasce com o sanitizado no mesmo INSERT; o lote é rejeitado e a limpeza da fase 3 tira os itens | B: CancelledWhileSanitizingLeavesNoItemWithoutSanitized |
| Cancelamento no meio | A reexecução cai no meio do conjunto | Uma transação por item: cada um tem texto e versão da mesma passagem; a tela mostra o que falta e oferece continuar | E: InterruptedRerunLeavesNoItemWithTextOfOneVersionAndRecordOfAnother (#97 CA-4) |
| Falha de rede | O serviço de linguagem não responde | Nenhuma análise, nenhum item: o lote é rejeitado com analysis_unavailable e a tela do lote diz por quê | A: LanguageServiceDownRejectsTheBatchAsAnalysisUnavailable |
| Falha de rede | O banco some durante a reexecução | O item da vez não muda; os já tratados ficam; a tela diz que a reexecução parou | E: DatabaseLostWhileRerunningKeepsTheItemsAlreadyTreated |
| Dado pessoal | Uma exceção num passo leva o texto ao log | O registro traz lote, posição do item, passo e tipo da exceção, nunca o texto | B: StepFailureLogCarriesNoMaterialText (#93 CA-4) |
| Dado pessoal | O relato do que foi removido mostra o removido | Só as contagens saem em claro; o rastro com as posições é cifrado e não vai à tela | B: ReportAndLogsOfASeededBatchCarryNoSeededValue (#93 CA-3) |
| Dado pessoal | O marcador revela o tamanho ou um pedaço do removido | O marcador é o mesmo para todo removido, sem caractere do original | B: RemovedValueLeavesAMarkerWithNoCharacterOfIt (#90 CA-1) |
| Dado pessoal | Uma cópia do banco expõe o sanitizado | O sanitizado e o rastro vão selados; a varredura de cópia não acha o texto semeado | B: DatabaseCopyHasNoReadableSanitizedText (#98 CA-2) |
| Dado pessoal | O sanitizado vai para a coluna em claro | A coluna do pseudonimizado fica nula em toda a fase; um teste a consulta ao final | B: ClearTextColumnHoldsNoMaterialAtTheEndOfThePhase (#98 CA-4) |
| Dado pessoal | A reexecução trabalha sobre o sanitizado | Ela decifra o bruto; item esvaziado, ou de chave perdida, é recusado com o motivo | E: RerunOfAnEmptiedItemIsRefusedWithTheReason (#97 CA-2) |

## O que fica de fora

- **Anotação desatualizada.** Não há anotação nesta fase; marcar a anotação afetada pela
  reexecução é da fase de anotação (#97, "O que esta issue não faz").
- **Pseudonimização.** O passo dos destinos deixa o pseudonimizado no texto e traduz a posição;
  trocar o nome é da fase 5.
- **Fila em segundo plano.** Não existe no código; a reexecução corre na requisição, em blocos,
  até a decisão do dono em contrário.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).
