# Fatia A — a entrada reconhece e grava os destinos

> Norma pendente. 2026-11-15. Issue nova; completa o CA-1 da #84. Teto da fatia: 600 linhas de
> produção.

A curadoria envia o arquivo como na fase 3, e cada item nasce com as ocorrências e o destino de
cada uma. Com o serviço de linguagem fora do ar, o lote é rejeitado sem itens, e a tela do lote
diz por quê. O protótipo é `lote.html`, estado "serviço de linguagem indisponível", em
[protótipos](prototipos/index.html).

Sem esta fatia, a #87 CA-5 não tem como ser cumprida: hoje nenhum item nasce com ocorrências.
`DecidedItemTexts.Of` existe e não tem chamador.

## Critérios de aceite

`Core.Host.Tests`, com o reconhecedor falso:

- EachUnitIsAnalyzedOnceInTheOrderOfTheMaterial
- EveryAnalysisIsAskedInPortuguese
- EveryItemIsBornWithItsDecidedOccurrences (#84 CA-1)
- AccommodatedItemKeepsItsAccommodationAndItsOccurrences
- LanguageServiceDownRejectsTheBatchAsAnalysisUnavailable
- MalformedAnalysisRejectsTheBatchAsAnalysisUnavailable
- CancelledWhileAnalyzingLeavesTheBatchRejectedWithNoItems
- RejectedForAnalysisShowsTheReasonOnTheBatchScreen

`Core.Persistence.Content.Tests`, com banco:

- OccurrencesAreSealedWithTheRawPurpose
- DestinationCountsMatchTheDecidedOccurrences (0056 da fase 3)

`Core.Host.Tests.Browser`:

- RejectedForAnalysisIsReadByKeyboardAndPassesTheAccessibilityCheck

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Análise na entrada | `SubmissionStoring`, `SubmissionJudgement`, `SubmissionService` | [002](../../padroes/002-servico-de-dominio.md) |
| Item com destinos | `DecidedItemTexts`, `NewItemText` | [002](../../padroes/002-servico-de-dominio.md) |
| Gravação | `ContentWriter`, sem mudança de esquema | [001](../../padroes/001-armazenamento.md) |
| Tela do lote | o motivo analysis_unavailable na I3 | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** cancelamento durante a análise e serviço de linguagem caído, nas linhas da fatia A
  em [riscos](riscos.md). O idioma é fixo em pt-BR: a I1 não tem campo de idioma, não há escolha
  por domínio, e todo pedido ao serviço de linguagem leva "pt".
- **Raia:** a submissão (`core/src/Core.Host/Submissions`) e a decisão de destino
  (`core/src/Core.Ingestion/Deciding`). Não cria pasta nova.
- **Depende de:** a fatia A da fase 3. **Bloqueia:** a fatia B.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
