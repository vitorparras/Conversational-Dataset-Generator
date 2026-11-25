# Fatia F — o referente pela correferência

> Norma pendente. 2026-11-23. F1 em dois PRs, a ferramenta antes da medição e a decisão com o
> registro depois; F2 e F3, um cada. Tetos: F1, 520 e depois o padrão; F2, 1.000; F3, 1.400 ([0054](../../../decisoes/0054-teto-por-fatia-e-migracao-por-data.md)).

O referente de cada menção é o grupo de correferência que o serviço de linguagem devolve, pela
regra por nome da [0060](../../../decisoes/0060-correferencia-na-resposta-da-fronteira.md). O
grupo cobre a conversa inteira, porque o escopo do marcador é a conversa ([0057](../../../decisoes/0057-escopo-do-marcador-e-a-conversa.md)): hoje o núcleo pede
um texto por vez (`SubmissionAnalysis`, linhas 24 a 27), e a F passa a um pedido por item. O autor
segue pelo nome que o material traz, dentro da conversa (`Message`, linha 9); a menção que o grupo
liga ao nome de um autor da mesma conversa recebe o marcador dele.

**F1 — modelo e medição.** O spaCy pt não tem correferência ([medição](../../../historico/003-medicao-do-modelo.md),
linha 18), e o Stanza udcoref, medido em conversas inteiras pelo [053](../../../operacao/053-medicao-da-correferencia.md),
ficou abaixo do limiar ([medição](../../../historico/005-medicao-da-correferencia.md)). A 0060
substitui a [0018](../../../decisoes/0018-contrato-da-fronteira.md) na forma da resposta com uma
regra por nome: mesmo texto, sem caixa e sem acento, é um grupo, e a menção de uma só palavra que é
parte de um nome mais longo entra no grupo dele se ele for o único candidato. Sem modelo, não há
tamanho máximo nem peso novo na [0041](../../../decisoes/0041-distribuicao-do-servico-de-linguagem.md).

**F2 — serviço e contrato.** O pedido leva os textos do item na ordem de `MaterialTexts`, cada
mensagem com o nome do autor. O serviço reconhece cada texto à parte, como a `/analyze`, aplica a
regra da 0060 ao item inteiro e devolve cada ocorrência no seu texto (índice, posições, tipo,
análise gramatical e grupo) e o grupo de cada nome de autor, que nunca é ocorrência; o autor é
candidato da parte do nome, e com dois a menção fica em grupo próprio. O `ResponseValidator` recusa
índice fora do item, posição fora do texto e grupo ausente; o `Core.Boundary.Fake` devolve grupos.

**F3 — o núcleo usa o grupo.** Um pedido por item; o grupo vira o referente da troca da fatia B, e
a F3 liga a troca à submissão e à reexecução da fase 4. Até ela, nenhum item recebe pseudonimizado.

**Primeiro pseudonimizado.** A F3 o dá, em segundo plano, pela fila e pelo trabalhador da reexecução
da fase 4 (`RerunService`), a todo item gravado antes dela. O serviço é chamado sobre o bruto só
pelo grupo; cada ocorrência guardada (`OccurrenceJson`, linha 37) toma o grupo da de mesmo texto,
posição e tipo. Item com ocorrência sem par é recusado, fica sem pseudonimizado e fora da anotação,
e o acompanhamento da fila o conta com o motivo; um pedido novo o põe na fila outra vez. Nenhum
destino é decidido de novo, com uma exceção declarada (`SanitizationRerun`, linhas 5 a 8): o
`Preserve` de lugar dos itens anteriores à fatia A vira `Pseudonymize` sem que o serviço o decida
([0056](../../../decisoes/0056-toponimo-vira-marcador.md)).

## Critérios de aceite

| Parte | Testes |
|---|---|
| F1 | CoreferenceMeasurementReportsPrecisionAndFusionApart; MeasurementCoversMessagesMixingLanguages; MeasurementReportsTheLinkToAuthorsApart; MeasurementReportsTimeAndMemoryByConversationSize; ModelBelowTheThresholdFailsTheMeasurement |
| F2 | OneRequestCarriesEveryTextOfTheItemWithItsAuthor; EveryOccurrenceComesBackInItsTextWithItsGroup; AuthorNamesComeBackWithTheirGroupAndNeverAsOccurrences; OccurrenceOutsideItsTextIsRefused; ResponseWithoutGroupsIsRefusedByTheNewContract; FakeServiceGivesGroups; OneWordMentionJoinsTheOnlyLongerNameHoldingIt; OneWordMentionWithTwoCandidateNamesKeepsItsOwnGroup |
| F3 | OneRequestPerItem; MentionsOfOneGroupShareTheMarkerAcrossMessages (#100 CA-1); MentionLinkedToAnAuthorGetsTheAuthorMarker; NicknameOfAnAuthorGetsItsOwnMarker; HomonymsInOneConversationShareTheMarker (#100 CA-2); PseudonymizedTextHasTheMarkersAndNoSeededOriginal (#102 CA-1); BatchRejectedForAnalysisLeavesNoPseudonymizedText (#102 CA-5); CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap; AuthorGetsAMarkerStoredWithTheItem; ResanitizedItemIsPseudonymizedAgainInTheSameTransaction; ItemWithoutPseudonymizedGetsItsFirstOne; PreservedPlaceOfAnOldItemBecomesAMarker; OldItemWithAnOccurrenceWithoutPairIsRefusedWithTheReason |

A F3 fecha a #102: os CA-1 e CA-5 só se provam pela submissão.

## Peças, raia e dependências

| Peça | Arquivos | Padrão |
|---|---|---|
| Medição e decisão (F1) | o procedimento 015, uma ferramenta em `language-service/tools`, a decisão nova | — |
| Contrato e serviço (F2) | `language-service/src/language_service/recognizers`, `Core.Boundary.Contracts`, `ResponseValidator` | [002](../../padroes/002-servico-de-dominio.md) |
| Núcleo (F3) | `SubmissionAnalysis`, `DecidedOccurrence`, a troca da fatia B, `RerunService` | [002](../../padroes/002-servico-de-dominio.md), [001](../../padroes/001-armazenamento.md) |

- **Riscos:** precisão, mistura de línguas, fusão e ligação ao autor, em [riscos](riscos.md).
- **Raia:** o serviço de linguagem e a fronteira (F1 e F2); a submissão e a reexecução (F3).
- **Depende de:** F1 e F2, da A; F3, de F2 e da B. **Bloqueia:** a E e o fim da fase.
