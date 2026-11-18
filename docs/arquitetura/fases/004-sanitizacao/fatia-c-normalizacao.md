# Fatia C — normalização sem achatar o sinal

> Norma pendente. 2026-11-15. Fecha #88, #89, #95 e #96. Teto da fatia: 1.000 linhas de
> produção.

Dois passos entram na lista, antes da aplicação dos destinos: o reparo da codificação e a retirada
do artefato de formato. A versão muda, e a tela do lote passa a mostrar as duas contagens novas.
As propriedades de preservação do sinal e de escopo passam a valer para todo passo da lista. O
protótipo é `lote.html`, em [protótipos](prototipos/index.html).

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` (#88) | SwappedAccentsReturnToTheirSpellingAndNothingElseChanges (CA-1); TextWithoutCorruptionLeavesIdentical (CA-2); EmojiCaseRepetitionQuotesAndLanguageMixSurviveTheRepair (CA-3); RepairTwiceEqualsRepairOnce (CA-4); AmbiguousRepairIsLeftAsWrittenAndCounted (CA-5) |
| `Core.Ingestion.Tests` (#89) | HeaderRepeatedOnEveryPageLeavesEveryPageAndNoOtherLine (CA-1); WrittenTextMatchingAnArtifactStaysAndIsCounted (CA-2); FormWithoutArtifactsLeavesUnchanged (CA-3); PlaceholderForMissingContentStays (CA-4); ArtifactRemovalTwiceEqualsOnce (CA-5) |
| `Core.Ingestion.Tests` (#95) | SlangMisspellingRepetitionAndExpressivePunctuationSurvive (CA-1, propriedade); EmojiSymbolAndLanguageMixSurvive (CA-2, propriedade); CaseAndIrregularSpacingDoNotChange (CA-3, propriedade); EverythingOutsideDeclaredEditsReappearsInOrder (CA-4, propriedade); StepThatFlattensAMarkIsRejectedEvenIfDeclared (CA-5) |
| `Core.Ingestion.Tests` (#96) | EveryDifferenceFallsWithinTheDeclaredScope (CA-1, propriedade); ChangeOutsideTheScopeIsRejectedWithSpanAndScope (CA-2); EveryRegisteredStepIsCoveredByTheConformance (CA-3); ChangeWithoutReturnedEditIsRejected (CA-4); WidenedScopePassesTheConformance (CA-5) |
| `Core.Host.Tests` | BatchScreenShowsTheRepairAndArtifactCounts |

As duas contagens de "não tratado" (#88 CA-5, #89 CA-2) são números no relato, como as demais: o
trecho ambíguo não sai do material cifrado.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Passos | dois arquivos novos na pasta Sanitizing | [002](../../padroes/002-servico-de-dominio.md) |
| Lista | a lista de passos, duas linhas | [002](../../padroes/002-servico-de-dominio.md) |
| Geradores | os geradores de texto do `PropertyRunner` | [002](../../padroes/002-servico-de-dominio.md) |
| Tela do lote | os rótulos das contagens novas na I3 | [004](../../padroes/004-tela.md) |

## Riscos, raia e dependências

- **Riscos:** passo que uniformiza o sinal sem dizer, contido pelas propriedades da #95 e da #96;
  nenhum risco de concorrência, rede ou cancelamento novo.
- **Raia:** os dois passos e os testes de propriedade. Divide com a D só a lista de passos.
- **Depende de:** a fatia B. **Bloqueia:** nada; a D roda em paralelo.

Ver [contratos](contratos.md) e [riscos](riscos.md).
