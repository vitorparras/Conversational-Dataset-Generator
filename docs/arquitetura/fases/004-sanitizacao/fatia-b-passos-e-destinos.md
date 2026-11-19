# Fatia B — passos declarados e aplicação dos destinos

> Norma pendente. 2026-11-15. Fecha #87, #90, #92, #93 e #98. Teto da fatia: 1.300 linhas de
> produção.

A sanitização roda síncrona na submissão, antes de gravar o item e, portanto, antes da amostra. Cada
item nasce com o sanitizado e o rastro, cifrados, e com a versão da lista de passos. A tela do lote
mostra a versão e só as contagens por passo; o rastro com as posições fica cifrado. A revisão por
amostra lê o sanitizado, com o marcador no lugar do removido: o tipo, da lista fixa dos
[contratos](contratos.md), nunca um caractere do original. Os protótipos são
`lote.html` e `revisao.html`, em [protótipos](prototipos/index.html).

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` (#87) | ResultCarriesSanitizedTextVersionAndStepOrder (CA-1); StepWithoutDeclaredScopeIsRefusedAtStartupByName (CA-2); ChangingTheStepOrderChangesTheVersion (CA-3); SameMaterialAndVersionGiveTheSameTextInAnyProcess (CA-4, propriedade); MaterialWithoutDecidedDestinationsIsRefused (CA-5) |
| `Core.Ingestion.Tests` (#90) | RemovedValueLeavesATypedMarkerWithNoCharacterOfIt (CA-1); PseudonymizedSpanStaysAndItsPositionResolves (CA-2); CredentialIsRemovedAndNeverOfferedAsPseudonym (CA-3); UnmarkedProperNameIsLeftAsWritten (CA-4); PreservedSpanLeavesAsItCame (CA-5) |
| `Core.Ingestion.Tests` (#92) | TranslatedPositionResolvesToTheSameSpanOrFailsVisibly (CA-1, propriedade); RemovedSpanTranslatesToNoLongerExists (CA-2); TranslationIsComposedFromReturnedEdits (CA-3); SanitizedSpanTranslatesBackToTheRawSpan (CA-4); PositionsCountCodePointsWithEmojiAndCombiningMarks (CA-5) |
| `Core.Ingestion.Tests` (#93) | ReportCountsEveryStepIncludingThoseWithZero (CA-1, CA-2) |
| `Core.Persistence.Content.Tests` | SanitizedAndTrailAreSealedWithTheRawPurpose (#98 CA-1, 0061); DatabaseCopyHasNoReadableSanitizedText (#98 CA-2); ClearTextColumnHoldsNoMaterialAtTheEndOfThePhase (#98 CA-4); SanitizedContentCannotBeFilteredOrOrderedInTheDatabase (#98 CA-5); SanitizationColumnsComeTogetherOrNotAtAll (0061); SameVersionFromTwoBatchesIsStoredOnce (0060); SanitizationEventCarriesTheVersionAndCannotChange (#93 CA-5, 0062); BatchSanitizationCountsReturnOnlyNumbers (0063) |
| `Core.Host.Tests` | SanitizedTextIsReadOnlyWithTheGrantAndTheReadIsRecorded (#98 CA-3); StepFailureLogCarriesNoMaterialText (#93 CA-4); ReportAndLogsOfASeededBatchCarryNoSeededValue (#93 CA-3); CancelledWhileSanitizingLeavesNoItemWithoutSanitized; BatchScreenShowsTheVersionAndTheCountsByStep; SampleIsDrawnOnlyAfterEveryItemIsSanitized; BatchScreenShowsNoPositionOfTheTrail; SampleReviewShowsTheSanitizedTextWithTheTypedMarker |
| `Core.Host.Tests.Browser` | SampleReviewOfSanitizedTextPassesTheAccessibilityCheck |

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migrações | `0060` a `0063` em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Passos e rastro | pasta nova Sanitizing em `core/src/Core.Ingestion` | [002](../../padroes/002-servico-de-dominio.md) |
| Gravação | `ContentWriter`, `ContentColumns`, `NewItemText` | [001](../../padroes/001-armazenamento.md) |
| Contagens | `BatchRegister` | [001](../../padroes/001-armazenamento.md) |
| Telas | a seção da sanitização na I3 (só contagens) e o texto na I4 | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** cancelamento entre itens, versão gravada por dois lotes, texto no log, marcador,
  cópia do banco e coluna em claro, nas linhas da fatia B em [riscos](riscos.md).
- **Raia:** a pasta Sanitizing, a gravação do item e as seções da I3 e da I4.
- **Requisito:** a fatia corrige a linha 38 do
  [requisito 003](../../../requisitos/003-copia-do-armazenamento-nao-expoe.md), que dispensa
  proteger o sanitizado; a [0030](../../../decisoes/0030-qual-texto-fica-em-claro.md) o cifra.
- **Depende de:** a fatia A e a fatia B da fase 3 (a 0059). **Bloqueia:** C, D e E.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
