# Fatia A — marcador estável, unidade do deslocamento e datas

> Norma pendente. 2026-11-22. Fecha #99, #100 e #108. Teto da fatia: 750 linhas de produção.

A fatia firma as três peças que a troca usa, antes de haver troca. O deslocamento que vem do serviço
só é recortado pelo conversor; o marcador é sorteado por tipo e se repete para o mesmo referente;
cada domínio ganha um deslocamento de datas, selado. Nada ainda grava pseudonimizado: a fatia é
verificada por propriedade e por banco. Não tem tela, e o deslocamento não aparece em tela alguma.

**Prova da estabilidade.** O mesmo referente com o mesmo mapa dá o mesmo marcador, em qualquer
processo: o conhecido vem do mapa do item, não do gerador. Dois referentes nunca dão o mesmo
marcador, nem em duas conversas: o sorteio só aceita número que nenhum domínio usou.

**Termos (#99 CA-3).** Invertido é fim antes do início; cruzado, na issue, é o mesmo par ("início e
fim não se cruzam"). Duas ocorrências que se sobrepõem não são recusa: a fase 4 as funde. Fim de uma
igual ao início da outra é adjacência, e as duas passam.

**Lugar.** Nome próprio de lugar vira marcador LUGAR, pela 0012 (linhas 14 e 15); o efeito nos itens
já gravados está em [riscos](riscos.md). O referente vem da [fatia F](fatia-f-correferencia.md).

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Boundary.Tests` (#99) | OffsetOutsideTheSentTextIsRefused (CA-2); EndBeforeStartIsRefusedTheSameWay (CA-3); AdjacentOccurrencesPassTheBoundary |
| `Core.Ingestion.Tests` (#99) | ReplacedSpanIsExactlyWhatTheRecognizerPointedPastEmojiAndCombiningMarks (CA-1); AdjacentOccurrencesAreBothReplaced |
| `Core.Tests.Architecture` (#99) | NoOffsetReachesTheReplacementWithoutTheConverter (CA-4: a troca só recorta por `ITextBoundaries`); OffsetUnitIsDeclaredOnceAndUsedToStore (CA-5) |
| `Core.Ingestion.Tests` (#100) | SameParticipantGetsTheSameMarkerWithinTheConversation (CA-1, propriedade); NoMarkerAppearsInTwoConversations (CA-1, propriedade); DistinctAuthorsWithTheSameNameGetDistinctMarkers (CA-2, propriedade); SameNameInTwoDomainsGetsUnrelatedMarkers (CA-3); MarkerCarriesNoInitialLengthNorAlphabeticalPosition (CA-4, propriedade); MarkerDoesNotRevealTheOrderOfAppearance (CA-4, propriedade); MarkerNamesTheKindOfTheMention (CA-5, pessoa, lugar e organização); ProperNameOfAPlaceIsPseudonymized (CA-5: `DestinationPolicy`, linhas 30 a 37, deixa de preservar lugar, e os casos de lugar de `DestinationPolicyTests`, linhas 28 e 41, passam a esperar `Pseudonymize`); RedrawnMarkerNeverRepeatsOneInUse |
| `Core.Ingestion.Tests` (#108) | ShiftKeepsEveryIntervalExact (CA-2, propriedade); LostShiftIsReportedAsDefinitive (CA-5) |
| `Core.Persistence.Content.Tests` (#108) | DateShiftIsDrawnOnceAndNeverChanges (CA-1); TwoDrawsAtOnceKeepTheFirstShift; TwoDomainsHaveDifferentShiftsWithNothingInCommon (CA-3); DatabaseCopyHasNoReadableDateShift |
| `Core.Host.Tests` (#99, #108) | OffsetOutsideTheTextRejectsTheBatchWithNoItem (#99 CA-2: o item não avança); ShiftValueAppearsInNoLogScreenOrResponse (#108 CA-4) |

O CA-3 da #100 vale pela unicidade na instalação. O deslocamento da #108 (CA-3) segue por sorteio
independente, sem unicidade: são poucos valores, e saber os usados ajudaria a adivinhar o de cada
domínio, que é segredo (0020, linha 42). O teste prova que nenhuma função recebe o de outro domínio.
O CA-4 da #100 mede a correlação do número com inicial, tamanho, posição alfabética e ordem de
chegada sobre originais sintéticos; o limiar é o do teste de independência que o plano fixa, nunca
relaxado. O CA-5 da #99 vale para gravar, e o da #108 para a leitura: exibir, exportar e reconciliar
são da fase 9 (as duas issues, "O que esta issue não faz").

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Recorte | `OffsetConverter`, `ResponseValidator`, `ITextBoundaries` | [002](../../padroes/002-servico-de-dominio.md) |
| Marcador | pasta nova Pseudonymizing em `core/src/Core.Ingestion`; `DestinationPolicy` | [002](../../padroes/002-servico-de-dominio.md) |
| Deslocamento de datas | a migração date-shift, `LabelPurpose`, um armazenamento ao lado de `ContentWriter` | [001](../../padroes/001-armazenamento.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md) |

## Riscos, raia e dependências

- **Riscos:** sorteio repetido, dois sorteios do deslocamento, marcador que revela, lugar no
  pseudonimizado e deslocamento que vaza, nas linhas da fatia A em [riscos](riscos.md).
- **Raia:** a fronteira, a política de destino, a pasta Pseudonymizing e a tabela nova.
- **Operação:** a finalidade nova pede um rótulo por domínio na custódia, como manda a 0013.
- **Depende de:** a fase 4 inteira. **Bloqueia:** B, F1 e F2.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
