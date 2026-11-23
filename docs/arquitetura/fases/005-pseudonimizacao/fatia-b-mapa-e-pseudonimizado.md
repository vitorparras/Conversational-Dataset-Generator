# Fatia B — mapa cifrado e gravação do pseudonimizado

> Norma pendente. 2026-11-22. Fecha #101 e #102. Teto da fatia: 1.400 linhas de produção.

A pseudonimização roda síncrona na submissão, logo depois da sanitização e antes de gravar o item.
Cada item nasce com o pseudonimizado, com o mapa dele (marcador e original, selado com a finalidade
do mapa) e com os marcadores em claro, o do autor incluído. A tela do lote ganha a seção da
pseudonimização: marcadores por tipo e itens com marcador, contados dos marcadores do item, só
números. A reexecução da fase 4 refaz o pseudonimizado e o mapa junto com o sanitizado. O protótipo
é `lote.html`, em [protótipos](prototipos/index.html).

**Pseudonimizado cifrado.** O pseudonimizado é selado com a chave do item, como mandam a
[0030](../../../decisoes/0030-qual-texto-fica-em-claro.md) (linha 24) e a
[0049](../../../decisoes/0049-chave-por-item-e-epoca.md) (linha 27); o
[roadmap](../../../roadmap/005-pseudonimizacao.md) (critério 5) e o CA-3 da #102 dizem o mesmo.

**Pendente do dono.** Abrir os mapas do domínio para achar o marcador existente
([contratos](contratos.md), fatia B).

**Prova da reexecução idêntica (#102 CA-2).** Rodar de novo no mesmo processo, sobre o mesmo
sanitizado e o mapa gravado na primeira vez: os marcadores vêm do mapa, então outro processo não
muda o resultado.

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | PseudonymizingAgainGivesTheSameBytesAndMarkers (#102 CA-2, propriedade); RemovedSpanNeverBecomesAMapEntry (#101 CA-5) |
| `Core.Persistence.Content.Tests` | DatabaseCopyHasNoReadableMapEntry (#101 CA-1); MapOfOneDomainDoesNotOpenWithTheKeyOfAnother (#101 CA-2); EveryMapOpeningAddsAnEventWithoutTheValue (#101 CA-3); MapCannotBeFilteredInTheDatabase e MapCannotBeOrderedInTheDatabase (#101 CA-4); DatabaseCopyReadsNeitherSanitizedNorPseudonymized (#102 CA-3, opção da 0030); PseudonymizedWithoutItsMapIsRefused e PseudonymizedWithoutTheEventIsRefused (#102 CA-4, uma condição do gatilho cada); ResanitizedItemIsPseudonymizedAgainInTheSameTransaction; ItemMarkersAreEmptiedWithTheItem; MarkerIsUniqueInTheDomain |
| `Core.Host.Tests` | EveryWritingPathRefusesTextThatWasNotPseudonymized (#102 CA-4); RemovedSpanIsRefusedFromTheMapAndTheAttemptIsRecorded (#101 CA-5); PseudonymizedTextHasTheMarkersAndNoSeededOriginal (#102 CA-1); BatchRejectedForAnalysisLeavesNoPseudonymizedText (#102 CA-5); TwoBatchesOfOneDomainAtOnceGiveOneMarkerPerReferent; CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap; AuthorGetsAMarkerStoredWithTheItem; BatchScreenShowsMarkerCountsByKindAndNoText |
| `Core.Host.Tests.Browser` | PseudonymizationSectionIsReadByKeyboardAndPassesTheAccessibilityCheck |

O CA-4 da #102 percorre os caminhos que escrevem o item (`ContentWriter.AddItemTextAsync`, a
reexecução e o esvaziamento) e, em cada um, tenta gravar o sanitizado onde vai o pseudonimizado,
sem a troca; o banco não lê o selado, então o que barra é o gatilho que exige o mapa e o evento. O
CA-5 da #101 tem dois testes: a troca lança antes de selar, e a submissão rejeita o lote com o
evento na cadeia.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | a pseudonymization em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Troca | a pasta Pseudonymizing da fatia A, depois de `Sanitizer` | [002](../../padroes/002-servico-de-dominio.md) |
| Gravação | `ContentWriter`, `ContentColumns`, `NewReversalMap`, `ItemResanitizer` | [001](../../padroes/001-armazenamento.md) |
| Leitura do mapa | `ProtectedContentReader`, um evento por mapa | [001](../../padroes/001-armazenamento.md) |
| Tela | a seção da pseudonimização na I3 (só números) | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** dois lotes com o mesmo referente novo, queda entre itens, reexecução, senha no mapa,
  cópia do banco e leitura do mapa, nas linhas da fatia B em [riscos](riscos.md).
- **Raia:** a submissão, a gravação do item, a reexecução da fase 4 e a seção nova da I3.
- **Depende de:** a fatia A. **Bloqueia:** C, e por ela D e E.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
