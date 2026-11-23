# Fatia A — marcador estável, unidade do deslocamento e datas

> Norma pendente. 2026-11-22. Fecha #99, #100 e #108. Teto da fatia: 700 linhas de produção.

A fatia firma as três peças que a troca usa, antes de haver troca. O deslocamento que vem do serviço
só é recortado pelo conversor; o marcador é sorteado por tipo e se repete para o mesmo referente;
cada domínio ganha um deslocamento de datas, selado. Nada ainda grava pseudonimizado: a fatia é
verificada por propriedade e por banco. Não tem tela, e o deslocamento não aparece em tela alguma.

**Prova da estabilidade.** O mesmo referente com o mesmo mapa dá o mesmo marcador, num só processo:
o marcador do referente conhecido vem do mapa, e não do gerador, então outro processo não muda
nada. Dois originais nunca dão o mesmo marcador, porque o sorteio exclui os usados no domínio.

**Termos (#99 CA-3).** Invertido é fim antes do início; cruzado, na issue, é o mesmo par ("início e
fim não se cruzam"). Duas ocorrências que se sobrepõem não são recusa: a fase 4 as funde. Fim de uma
igual ao início da outra é adjacência, e as duas passam.

**Pendentes do dono.** O referente (contratos, fatia A) e o lugar: `DestinationPolicy` (linhas 30 a
37) preserva nome de lugar, então o CA-5 da #100 se prova para pessoa e organização até a decisão.

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Boundary.Tests` (#99) | OffsetOutsideTheSentTextIsRefused (CA-2); EndBeforeStartIsRefusedTheSameWay (CA-3); AdjacentOccurrencesPassTheBoundary |
| `Core.Ingestion.Tests` (#99) | ReplacedSpanIsExactlyWhatTheRecognizerPointedPastEmojiAndCombiningMarks (CA-1); AdjacentOccurrencesAreBothReplaced |
| `Core.Tests.Architecture` (#99) | NoOffsetReachesTheReplacementWithoutTheConverter (CA-4: a troca só recorta por `ITextBoundaries`); OffsetUnitIsDeclaredOnceAndUsedToStore (CA-5) |
| `Core.Ingestion.Tests` (#100) | SameParticipantGetsTheSameMarkerAcrossConversationsOfADomain (CA-1, propriedade); DistinctAuthorsWithTheSameNameGetDistinctMarkers (CA-2, propriedade); SameNameInTwoDomainsGetsUnrelatedMarkers (CA-3); MarkerCarriesNoInitialLengthNorAlphabeticalPosition (CA-4, propriedade); MarkerDoesNotRevealTheOrderOfAppearance (CA-4, propriedade); MarkerNamesTheKindOfTheMention (CA-5); RedrawnMarkerNeverRepeatsOneInUse |
| `Core.Ingestion.Tests` (#108) | ShiftKeepsEveryIntervalExact (CA-2, propriedade); LostShiftIsReportedAsDefinitive (CA-5) |
| `Core.Persistence.Content.Tests` (#108) | DateShiftIsDrawnOnceAndNeverChanges (CA-1); TwoDrawsAtOnceKeepTheFirstShift; TwoDomainsHaveDifferentShiftsWithNothingInCommon (CA-3); DatabaseCopyHasNoReadableDateShift |
| `Core.Host.Tests` (#99, #108) | OffsetOutsideTheTextRejectsTheBatchWithNoItem (#99 CA-2: o item não avança); ShiftValueAppearsInNoLogScreenOrResponse (#108 CA-4) |

O CA-3 da #100 e o CA-3 da #108 pedem valores diferentes entre domínios, e os sorteios são
independentes: o teste prova que nenhuma função recebe o valor de outro domínio, e a coincidência
por acaso (um em um milhão no marcador) é pendente do dono. O CA-4 da #100 mede a correlação do
número com inicial, tamanho, posição alfabética e ordem de chegada sobre originais sintéticos; o
limiar é o do teste de independência que o plano fixa, nunca relaxado. O CA-5 da #99 vale para
gravar, e o da #108 para a leitura: exibir, exportar e reconciliar são da fase 9 (as duas issues,
"O que esta issue não faz").

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Recorte | `OffsetConverter`, `ResponseValidator`, `ITextBoundaries` | [002](../../padroes/002-servico-de-dominio.md) |
| Marcador | pasta nova Pseudonymizing em `core/src/Core.Ingestion` | [002](../../padroes/002-servico-de-dominio.md) |
| Deslocamento de datas | a migração date-shift, `LabelPurpose`, um armazenamento ao lado de `ContentWriter` | [001](../../padroes/001-armazenamento.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md) |

## Riscos, raia e dependências

- **Riscos:** sorteio repetido, dois sorteios do deslocamento, marcador que revela e deslocamento
  que vaza, nas linhas da fatia A em [riscos](riscos.md).
- **Raia:** a fronteira, a pasta Pseudonymizing e a tabela nova. Não toca a submissão.
- **Operação:** a finalidade nova pede um rótulo por domínio na custódia, como manda a 0013.
- **Depende de:** a fase 4 inteira. **Bloqueia:** a fatia B.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
