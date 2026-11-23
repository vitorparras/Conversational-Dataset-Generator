# Riscos da fase 5

> Norma pendente. 2026-11-22. Cada risco tem o teste que o ataca, com o nome da fatia.

A troca é domínio puro: o mesmo sanitizado e o mesmo mapa dão o mesmo pseudonimizado. O sorteio é a
única parte não determinística, e ele só roda para referente que o mapa ainda não tem. Os riscos de
concorrência ficam na gravação do item, transação serializável como na fase 4, e nos atos da
reversão, cada um uma linha nova com o evento na mesma transação.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | Dois lotes do mesmo domínio trazem o mesmo referente novo e sorteiam dois marcadores | A troca toma uma trava de transação por domínio antes de abrir os mapas; o segundo lote espera e acha o marcador do primeiro | B: TwoBatchesOfOneDomainAtOnceGiveOneMarkerPerReferent |
| Concorrência | Dois sorteios dão o mesmo número no domínio | content.domain_marker é único por domínio; o sorteio exclui os usados e, perdendo, sorteia de novo | A: RedrawnMarkerNeverRepeatsOneInUse |
| Concorrência | Duas pessoas autorizam o mesmo pedido ao mesmo tempo | O gatilho da migração reversal-request recusa a segunda autorização enquanto a primeira vale, em transação serializável | C: SecondAuthorizationWhileOneIsValidIsRefused |
| Concorrência | O POST de revelar chega duas vezes | A revelação é única por autorização; a segunda perde e é recusada como já revelada | D: TwoRevealPostsAtOnceRevealOnce |
| Concorrência | Dois sorteios do deslocamento de datas do mesmo domínio | O índice único deixa o primeiro; o segundo lê o que ficou | A: TwoDrawsAtOnceKeepTheFirstShift |
| Falha no meio | A requisição cai entre dois itens pseudonimizados | O item nasce com o pseudonimizado, o mapa e os marcadores no mesmo INSERT; o lote é rejeitado e a limpeza da fase 3 tira os itens | B: CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap |
| Falha no meio | A reexecução da fase 4 troca o sanitizado e não o pseudonimizado | O gatilho de item_resanitized exige os dois e o mapa novo na mesma transação | B: ResanitizedItemIsPseudonymizedAgainInTheSameTransaction |
| Falha no meio | O evento da revelação não é gravado | A identidade só é lida depois de a transação do evento confirmar; falha deixa o pedido sem atendimento | D: FailureToRecordTheEventRevealsNothingAndLeavesTheRequestOpen (#105 CA-2) |
| Falha de rede | O serviço de linguagem não responde | Lote rejeitado na entrada, como na fase 4: nenhum item e nenhum pseudonimizado | B: BatchRejectedForAnalysisLeavesNoPseudonymizedText (#102 CA-5) |
| Prazo vencido | Uma autorização vencida ainda revela | O POST confere o prazo na mesma transação que grava a revelação; vencida, nega e grava a negativa | D: ExpiredAuthorizationDeniesAsIfItNeverExistedAndIsRecorded (#104 CA-2) |
| Cadeia quebrada | Uma reversão é autorizada com a cadeia do domínio quebrada | A gravação já é recusada com a cadeia quebrada (`CustodyRefusals`, linha 27); a tela diz por quê | C: BrokenChainRefusesNewAuthorizationsInTheDomain (#104 CA-5) |
| Dado pessoal | O marcador revela algo do original | O número é sorteado, sem relação com o nome nem com a ordem de chegada | A: MarkerCarriesNoInitialLengthNorAlphabeticalPosition; MarkerDoesNotRevealTheOrderOfAppearance (#100 CA-4) |
| Dado pessoal | Uma senha vai para o mapa | Entrada de destino remover lança antes de selar; o lote é rejeitado e o evento fica | B: RemovedSpanIsRefusedFromTheMapAndTheAttemptIsRecorded (#101 CA-5) |
| Dado pessoal | Uma cópia do banco expõe o mapa | As entradas vão seladas com a finalidade do mapa e a chave da linha | B: DatabaseCopyHasNoReadableMapEntry (#101 CA-1) |
| Dado pessoal | Achar o marcador existente abre os mapas sem registro | A leitura grava reversal_map_lookup antes de abrir, um por lote | B: MapLookupIsRecordedOncePerBatchBeforeOpening (#101 CA-3) |
| Dado pessoal | A identidade revelada fica no registro, no log ou em cache | Nenhuma coluna a guarda; a página sai sem cache e o GET não a mostra de novo | D: RevealedValueIsInNoTableLogOrCache (#105 CA-4); RevealedPageIsNotCachedAndIsNotShownAgain (#616 CA-2) |
| Dado pessoal | Uma leitura nova devolve o original sem reversão | A varredura semeia e procura em toda leitura do inventário; leitura fora dele reprova | E: NoReadWithoutReversalReturnsASeededValue; ContentReadOutsideTheInventoryFailsTheCheck |
| Dado pessoal | Uma exceção na troca leva o texto ao log | O registro traz item, passo e motivo; a redação do núcleo apaga o campo marcado | E: ExceptionWhilePseudonymizingLogsItemStepAndReasonOnly (#107 CA-2) |
| Dado pessoal | O deslocamento de datas vaza | Selado no banco; nenhuma tela o mostra e nenhum registro o leva | A: ShiftValueAppearsInNoLogScreenOrResponse (#108 CA-4) |

## O que fica de fora

- **Exportação.** Aplicar o deslocamento de datas e declarar a unidade no artefato é da fase 9.
- **Concessão por domínio e papéis.** A recusa por função usa `PersonFunctions` e os domínios
  concedidos de hoje; a fase 6 os consolida (#103, "O que esta issue não faz").
- **Classificação da quebra.** O código recusa a gravação enquanto a última verificação falhou; a
  classificação da [verificação](../../../privacidade/005-verificacao-da-custodia.md) não existe.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).
