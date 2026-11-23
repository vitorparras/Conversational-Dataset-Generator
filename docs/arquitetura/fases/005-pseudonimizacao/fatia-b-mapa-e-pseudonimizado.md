# Fatia B — mapa cifrado e gravação do pseudonimizado

> Norma pendente. 2026-11-22. Fecha #101. Teto da fatia: 1.200 linhas de produção.

A troca roda síncrona na submissão, logo depois da sanitização e antes de gravar o item, quando a
[fatia F](fatia-f-correferencia.md) a ligar. Cada item nasce com o pseudonimizado, com o mapa dele
(marcador e original, selado com a finalidade do mapa) e com os marcadores em claro, o do autor
incluído. A tela do lote ganha a seção da pseudonimização: marcadores por tipo e itens com marcador,
contados dos marcadores do item, só números. A reexecução da fase 4 refaz o pseudonimizado e o mapa
junto com o sanitizado. O protótipo é `lote.html`, em [protótipos](prototipos/index.html).

**Pseudonimizado cifrado** com a chave do item: [0030](../../../decisoes/0030-qual-texto-fica-em-claro.md)
(linha 24), [0049](../../../decisoes/0049-chave-por-item-e-epoca.md) (linha 27) e CA-3 da #102.

**O que a F espera da B.** A troca recebe o referente de cada ocorrência pronto, e a B a prova com o
referente dado no teste. O grupo que vira referente, a ligação da troca à submissão e à reexecução,
o gatilho de item_resanitized e os testes que passam pela submissão são da F3, que fecha a #102.

**Reexecução e marcadores em uso.** A reexecução mantém as linhas do item em content.marker_in_use e
em content.item_marker, sem sortear de novo, e insere só o marcador novo. O marcador que some do
texto sai de item_marker e continua reservado em marker_in_use, para nunca voltar em outra conversa.

**Prova da reexecução idêntica (#102 CA-2).** Rodar de novo no mesmo processo, sobre o mesmo
sanitizado e o mapa do item, aberto com o evento de leitura: os marcadores vêm do mapa, então outro
processo não muda o resultado. Nenhum outro mapa é aberto ([contratos](contratos.md), fatia B).

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | PseudonymizingAgainGivesTheSameBytesAndMarkers (#102 CA-2, propriedade); SwapTakesNoMapButTheOneOfItsItem; RemovedSpanNeverBecomesAMapEntry (#101 CA-5) |
| `Core.Persistence.Content.Tests` | DatabaseCopyHasNoReadableMapEntry (#101 CA-1); MapOfOneDomainDoesNotOpenWithTheKeyOfAnother (#101 CA-2); EveryMapOpeningAddsAnEventWithoutTheValue (#101 CA-3); MapCannotBeFilteredInTheDatabase e MapCannotBeOrderedInTheDatabase (#101 CA-4); DatabaseCopyReadsNeitherSanitizedNorPseudonymized (#102 CA-3); PseudonymizedWithoutItsMapIsRefused e PseudonymizedWithoutTheEventIsRefused (#102 CA-4, uma condição do gatilho cada); RemovedSpanIsRefusedFromTheMapAndTheAttemptIsRecorded (#101 CA-5); ItemMarkersAreEmptiedWithTheItem; MarkerIsUniqueInTheInstallation; ResanitizedItemKeepsItsMarkersWithoutDrawingAgain; MarkerGoneFromTheTextStaysReserved; DrawLosingToAnotherDomainDrawsAgainWithoutReadingIt |
| `Core.Host.Tests` | EveryWritingPathRefusesTextThatWasNotPseudonymized (#102 CA-4); BatchScreenShowsMarkerCountsByKindAndNoText |
| `Core.Host.Tests.Browser` | PseudonymizationSectionIsReadByKeyboardAndPassesTheAccessibilityCheck |

O CA-4 da #102 percorre os caminhos que escrevem o item (`ContentWriter.AddItemTextAsync`, a
reexecução e o esvaziamento) e, em cada um, tenta gravar o sanitizado onde vai o pseudonimizado,
sem a troca; o banco não lê o selado, então o que barra é o gatilho que exige o mapa e o evento. O
CA-5 da #101 tem dois testes: a troca lança antes de selar, e o gravador recusa a entrada e grava o
evento na cadeia.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | a pseudonymization em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Troca | a pasta Pseudonymizing da fatia A, depois de `Sanitizer` | [002](../../padroes/002-servico-de-dominio.md) |
| Gravação | `ContentWriter`, `ContentColumns`, `NewReversalMap` | [001](../../padroes/001-armazenamento.md) |
| Leitura do mapa | `ProtectedContentReader`, um evento por mapa, só na reexecução e na revelação | [001](../../padroes/001-armazenamento.md) |
| Tela | a seção da pseudonimização na I3 (só números) | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** senha no mapa, cópia do banco e leitura do mapa, nas linhas da fatia B em
  [riscos](riscos.md).
- **Raia:** a troca, a gravação do item e a seção nova da I3.
- **Depende de:** a fatia A. **Bloqueia:** C, e por ela D e E, e a F3.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
