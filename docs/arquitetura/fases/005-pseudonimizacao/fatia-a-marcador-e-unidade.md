# Fatia A — marcador estável, unidade do deslocamento e datas

> Norma pendente. 2026-11-22. Fecha #99, #100 e #108. Teto da fatia: 700 linhas de produção.

A fatia firma as três peças que a troca usa, antes de haver troca. O deslocamento que vem do serviço
só chega à troca pelo conversor; o marcador é sorteado por tipo e se repete para o mesmo referente;
cada domínio ganha um deslocamento de datas, selado. Nada ainda grava pseudonimizado: a fatia é
verificada por propriedade e por banco. Não tem tela; o deslocamento de datas não aparece em tela
alguma (#108 CA-4).

**Prova do sorteio e da estabilidade.** O mesmo referente com o mesmo mapa dá o mesmo marcador, num
só processo: o marcador do referente conhecido vem do mapa, e não do gerador, então outro processo
não muda nada. Dois originais nunca dão o mesmo marcador porque o sorteio exclui os usados.

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Boundary.Tests` (#99) | ReplacedSpanIsExactlyWhatTheRecognizerPointedPastEmojiAndCombiningMarks (CA-1); OffsetOutsideTheSentTextIsRefusedAndTheItemDoesNotAdvance (CA-2); InvertedOrCrossedOffsetsAreRefusedTheSameWay (CA-3) |
| `Core.Ingestion.Tests` (#99) | AdjacentOccurrencesAreBothReplaced (fim de uma igual ao início da outra) |
| `Core.Tests.Architecture` (#99) | NoOffsetReachesTheReplacementWithoutTheConverter (CA-4); OffsetUnitIsDeclaredOnceAndUsedToStore (CA-5) |
| `Core.Ingestion.Tests` (#100) | SameParticipantGetsTheSameMarkerAcrossConversationsOfADomain (CA-1, propriedade); DistinctAuthorsWithTheSameNameGetDistinctMarkers (CA-2, propriedade); SameNameInTwoDomainsGetsUnrelatedMarkers (CA-3); MarkerCarriesNoInitialLengthNorAlphabeticalPosition (CA-4, propriedade); MarkerDoesNotRevealTheOrderOfAppearance (CA-4, propriedade); MarkerNamesTheKindOfTheMention (CA-5); RedrawnMarkerNeverRepeatsOneInUse |
| `Core.Ingestion.Tests` (#108) | ShiftKeepsEveryIntervalExact (CA-2, propriedade); LostShiftIsReportedAsDefinitive (CA-5) |
| `Core.Persistence.Content.Tests` (#108) | DateShiftIsDrawnOnceAndNeverChanges (CA-1); TwoDrawsAtOnceKeepTheFirstShift; TwoDomainsHaveDifferentShiftsWithNothingInCommon (CA-3); DatabaseCopyHasNoReadableDateShift |
| `Core.Host.Tests` (#108) | ShiftValueAppearsInNoLogScreenOrResponse (CA-4) |

O CA-3 da #100 compara os dois marcadores e confere que nenhuma função do domínio recebe o marcador
de outro domínio. O CA-4 sorteia originais sintéticos e mede a correlação do número com a inicial,
o tamanho, a posição alfabética e a ordem de chegada; o limiar é o do teste de independência que o
plano fixa, nunca relaxado para passar. O CA-5 da #99 vale para gravar; exibir e exportar não
existem ainda, e a fase 9 declara a unidade no artefato. O CA-5 da #108 prova a leitura da
aplicação; reconciliar artefato exportado é da fase 9.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Trecho em ponto de código | `OffsetConverter`, `ResponseValidator` e o tipo novo do trecho, em `core/src/Core.Boundary` | [002](../../padroes/002-servico-de-dominio.md) |
| Marcador | pasta nova Pseudonymizing em `core/src/Core.Ingestion` | [002](../../padroes/002-servico-de-dominio.md) |
| Deslocamento de datas | a migração date-shift, `LabelPurpose`, um armazenamento ao lado de `ContentWriter` | [001](../../padroes/001-armazenamento.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md) |

## Riscos, raia e dependências

- **Riscos:** sorteio repetido, dois sorteios do deslocamento, marcador que revela e deslocamento
  que vaza, nas linhas da fatia A em [riscos](riscos.md).
- **Raia:** a fronteira (`core/src/Core.Boundary`), a pasta Pseudonymizing e a tabela nova. Não
  toca a submissão.
- **Operação:** a finalidade nova pede um rótulo por domínio na custódia, como manda a 0013; o
  plano diz como o rótulo entra no arquivo de rótulos.
- **Depende de:** a fase 4 inteira. **Bloqueia:** a fatia B.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
