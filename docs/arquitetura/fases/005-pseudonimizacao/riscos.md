# Riscos da fase 5

> Norma pendente. 2026-11-22. Cada risco tem o teste que o ataca, com o nome da fatia.

A troca é domínio puro: o mesmo sanitizado e o mesmo mapa dão o mesmo pseudonimizado. O sorteio é a
única parte não determinística, e ele só roda para referente que a conversa ainda não tem. Os riscos
de concorrência ficam na gravação do item, transação serializável como na fase 4, e nos atos da
reversão, cada um uma linha nova com o evento na mesma transação.

| Risco | O que pode acontecer | Como o desenho responde | Teste |
|---|---|---|---|
| Concorrência | Dois itens, do mesmo domínio ou de outros, sorteiam o mesmo número | content.marker_in_use é única na instalação; o sorteio insere sem ler os outros domínios e, perdendo no índice, sorteia de novo | A: RedrawnMarkerNeverRepeatsOneInUse; B: MarkerIsUniqueInTheInstallation; DrawLosingToAnotherDomainDrawsAgainWithoutReadingIt |
| Concorrência | Duas pessoas autorizam o mesmo pedido ao mesmo tempo | O gatilho da reversal-request recusa a segunda autorização em vigor, em transação serializável | C: SecondAuthorizationWhileOneIsValidIsRefused |
| Concorrência | O POST de revelar chega duas vezes | A revelação é única por autorização; a segunda perde e é negada como já revelada | D: TwoRevealPostsAtOnceRevealOnce |
| Concorrência | Dois sorteios do deslocamento de datas do mesmo domínio | O índice único deixa o primeiro; o segundo lê o que ficou | A: TwoDrawsAtOnceKeepTheFirstShift |
| Falha no meio | A requisição cai entre dois itens pseudonimizados | O item nasce com o pseudonimizado, o mapa e os marcadores no mesmo comando; o lote é rejeitado e a limpeza da fase 3 tira os itens | F3: CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap |
| Falha no meio | A reexecução da fase 4 troca o sanitizado e não o pseudonimizado | O gatilho de item_resanitized exige os dois e o mapa novo na mesma transação | F3: ResanitizedItemIsPseudonymizedAgainInTheSameTransaction |
| Falha no meio | O evento da revelação não é gravado | A identidade só é lida depois de a transação do evento confirmar; falha deixa o pedido sem atendimento | D: FailureToRecordTheEventRevealsNothingAndLeavesTheRequestOpen (#105 CA-2) |
| Falha de rede | O serviço de linguagem não responde | Lote rejeitado na entrada, como na fase 4: nenhum item e nenhum pseudonimizado | F3: BatchRejectedForAnalysisLeavesNoPseudonymizedText (#102 CA-5) |
| Prazo vencido | Uma autorização vencida ainda revela | O POST confere o prazo na transação que grava a revelação; vencida, nega e grava a negativa | D: ExpiredAuthorizationDeniesAsIfItNeverExistedAndIsRecorded (#104 CA-2) |
| Cadeia quebrada | Uma reversão é autorizada com a cadeia quebrada | A cadeia é uma só na instalação, e com ela quebrada nenhuma gravação passa (`CustodyRefusals`, linha 27), nem a recusa; a tela diz por quê | C: BrokenChainRefusesNewAuthorizations (#104 CA-5) |
| Dado pessoal | Nome de lugar fica no pseudonimizado | `DestinationPolicy` passa a pseudonimizar nome próprio de lugar, com o prefixo LUGAR (0012, linhas 14 e 15); nenhuma ocorrência nova recebe `Preserve`, e o `Preserve` de lugar dos itens antigos vira `Pseudonymize` no primeiro pseudonimizado | A: ProperNameOfAPlaceIsPseudonymized (#100 CA-5); F3: PreservedPlaceOfAnOldItemBecomesAMarker |
| Precisão | O grupo de correferência liga menções de entidades distintas, ou separa as da mesma, em pt-BR | A F1 mede a precisão em pt-BR; a 0060 fixa o limiar, e o Stanza udcoref, abaixo dele, não entra. O grupo sai da regra por nome: a menção de uma só palavra só entra no grupo de um nome mais longo se ele for o único candidato | F1: CoreferenceMeasurementReportsPrecisionAndFusionApart; ModelBelowTheThresholdFailsTheMeasurement; F2: OneWordMentionJoinsTheOnlyLongerNameHoldingIt; OneWordMentionWithTwoCandidateNamesKeepsItsOwnGroup |
| Precisão | Mensagem que mistura línguas passa pelo modelo de pt (`SubmissionAnalysis`, linha 14), e o grupo erra no trecho da outra língua | A medição da F1 inclui essas mensagens, cujo sinal o [requisito 005](../../../requisitos/005-sanitizacao-preserva-o-sinal.md) e a política 002 (linha 17) mandam preservar, e a precisão nelas entra no limiar | F1: MeasurementCoversMessagesMixingLanguages |
| Dado pessoal | Duas pessoas distintas de mesmo nome na mesma conversa caem no mesmo grupo e dividem o marcador | Limite declarado da regra por nome (0060), que o CA-2 da #100 assume; o grupo vale só dentro da conversa, e em conversas distintas os marcadores seguem distintos | F3: HomonymsInOneConversationShareTheMarker (#100 CA-2); A: NoMarkerAppearsInTwoConversations |
| Precisão | A correferência liga a menção ao nome do autor errado, ou não liga | A F1 mede a ligação ao autor à parte, e o Stanza udcoref acerta 0,243; pela regra da 0060, a menção liga ao autor pelo nome, e a que não casa, como o apelido, recebe marcador próprio, distinto do dele | F1: MeasurementReportsTheLinkToAuthorsApart; F3: MentionLinkedToAnAuthorGetsTheAuthorMarker; MentionOfAnAuthorWithTheLinkOffGetsItsOwnMarker |
| Precisão | Conversa longa demais para um pedido | A F1 mede tempo e memória pelo tamanho da conversa; a regra por nome não carrega modelo, e a 0060 não fixa máximo, que volta com um modelo | F1: MeasurementReportsTimeAndMemoryByConversationSize |
| Dado pessoal | O mesmo marcador em duas conversas liga pessoas que o sistema não sabe ligar | Escopo na conversa (0057) e unicidade na instalação | A: NoMarkerAppearsInTwoConversations; B: MarkerIsUniqueInTheInstallation |
| Dado pessoal | Item antigo ganha pseudonimizado com ocorrência que a resposta de agora não traz | Sem par de mesmo texto, posição e tipo, o item é recusado e fica sem pseudonimizado, fora da anotação; o acompanhamento da fila o conta com o motivo, e um pedido novo o põe na fila outra vez | F3: OldItemWithAnOccurrenceWithoutPairIsRefusedWithTheReason |
| Dado pessoal | O marcador revela algo do original | O número é sorteado, sem relação com o nome nem com a ordem de chegada | A: MarkerCarriesNoInitialLengthNorAlphabeticalPosition; MarkerDoesNotRevealTheOrderOfAppearance (#100 CA-4) |
| Dado pessoal | Uma senha vai para o mapa | Entrada de destino remover lança antes de selar; o gravador a recusa e o evento fica | B: RemovedSpanIsRefusedFromTheMapAndTheAttemptIsRecorded (#101 CA-5) |
| Dado pessoal | Uma cópia do banco expõe o mapa ou o porquê do pedido | As entradas e o texto do pedido vão selados com a finalidade do mapa e a chave da linha | B: DatabaseCopyHasNoReadableMapEntry (#101 CA-1); C: DatabaseCopyHasNoReadableRequestText |
| Dado pessoal | A reexecução abre o mapa do item sem registro, ou abre outro mapa | Só a reexecução e a revelação abrem mapa, e só o do item, com o evento de leitura antes | B: EveryMapOpeningAddsAnEventWithoutTheValue (#101 CA-3); SwapTakesNoMapButTheOneOfItsItem |
| Dado pessoal | A identidade revelada fica no registro, no log ou em cache | Nenhuma coluna a guarda; a página sai sem cache e o GET não a mostra de novo | D: RevealedValueIsInNoTableLogOrCache (#105 CA-4); RevealedPageIsNotCachedAndIsNotShownAgain (#616 CA-2) |
| Dado pessoal | Uma leitura nova devolve o original sem reversão | A varredura semeia e procura em toda leitura do inventário; leitura fora dele reprova | E: NoReadWithoutReversalReturnsASeededValue; ContentReadOutsideTheInventoryFailsTheCheck |
| Dado pessoal | Uma exceção na troca leva o texto ao log | O registro traz lote, posição do item, passo e motivo; a redação do núcleo apaga o campo marcado | E: ExceptionWhilePseudonymizingLogsBatchPositionStepAndReasonOnly (#107 CA-2) |
| Dado pessoal | O deslocamento de datas vaza | Selado no banco; nenhuma tela o mostra e nenhum registro o leva | A: ShiftValueAppearsInNoLogScreenOrResponse (#108 CA-4) |

## O que fica de fora

- **Exportação.** Aplicar o deslocamento de datas e declarar a unidade no artefato é da fase 9,
  como dizem a #99 e a #108 em "O que esta issue não faz".
- **Concessão por domínio e papéis.** A recusa por função usa `PersonFunctions` e os domínios
  concedidos de hoje; a fase 6 os consolida (#103, "O que esta issue não faz").
- **Classificação da quebra.** O código recusa a gravação enquanto a última verificação falhou; a
  classificação da [verificação](../../../privacidade/005-verificacao-da-custodia.md) não existe.

Ver [modelo de dados](modelo-de-dados.md) e [contratos](contratos.md).

## Itens gravados antes da F3

Nenhum item tem pseudonimizado antes da F3, que o dá a cada um em segundo plano
([fatia F](fatia-f-correferencia.md)). Os gravados antes da fatia A guardam o lugar como `Preserve`
no destino selado (`OccurrenceJson`, linha 37), e a política não entra na versão da lista de passos
(`SanitizationSteps`, linhas 23 a 25). Nesse primeiro pseudonimizado, o `Preserve` de lugar vira
`Pseudonymize` sem que o serviço o decida, exceção declarada à regra da reexecução
(`SanitizationRerun`, linhas 5 a 8), pela [0056](../../../decisoes/0056-toponimo-vira-marcador.md).
