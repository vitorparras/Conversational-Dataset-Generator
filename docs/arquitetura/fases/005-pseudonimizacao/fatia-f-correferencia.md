# Fatia F — o referente pela correferência

> Norma pendente. 2026-11-23. Três partes, F1, F2 e F3, cada uma num PR. Tetos: F1, 500 linhas;
> F2, 700; F3, 1.400 ([0054](../../../decisoes/0054-teto-por-fatia-e-migracao-por-data.md)).

O referente de cada menção é o grupo de correferência que o serviço de linguagem devolve: o serviço
diz quais menções do texto enviado são a mesma entidade. Não há reserva por nome padronizado. O
núcleo envia um texto por mensagem (`SubmissionAnalysis`, linhas 24 a 27), então o grupo liga
menções dentro da mesma mensagem; o marcador de quem já apareceu fora dela segue o escopo do
marcador e os [contratos](contratos.md) (fatia B), em aberto. O autor da mensagem não é menção: o
referente dele é o autor que o material traz (`Message`, linha 9).

**F1 — modelo e medição.** Os modelos pt do spaCy medidos não têm componente de correferência
([medição](../../../historico/003-medicao-do-modelo.md), linha 18), então F1 escolhe o modelo, mede
a precisão em pt-BR pelo procedimento de [015](../../../operacao/015-medicao-do-modelo-de-linguagem.md)
e escreve a decisão nova, no formato de [decisões](../../../decisoes/README.md), que substitui a
[0018](../../../decisoes/0018-contrato-da-fronteira.md) na forma da resposta: cada ocorrência ganha
o campo de grupo. A decisão fixa o limiar; modelo abaixo dele não entra. O modelo tem de caber na
[0041](../../../decisoes/0041-distribuicao-do-servico-de-linguagem.md) e operar sem serviço externo.

**F2 — serviço.** O serviço devolve o grupo de cada ocorrência, na versão nova do contrato; o
`ResponseValidator` recusa grupo ausente ou que aponte ocorrência inexistente, e o serviço falso
(`Core.Boundary.Fake`) passa a devolver grupo.

**F3 — o núcleo usa o grupo.** O grupo vira o referente da troca da fatia B, e a F3 liga a troca à
submissão e à reexecução da fase 4. Até ela, nenhum item recebe pseudonimizado, como na fase 4.

**Primeiro pseudonimizado.** A F3 o dá, em segundo plano, pela fila e pelo trabalhador da
reexecução da fase 4 (`RerunService`), a todo item sem ele: os gravados antes da F3. O serviço é
chamado sobre o bruto só pelo grupo; cada ocorrência guardada (`OccurrenceJson`, linha 37) toma o
grupo da ocorrência de mesma posição e tipo, e item com ocorrência sem par é recusado com o motivo e
fica sem pseudonimizado. Nenhum destino é decidido de novo, com uma exceção declarada à regra da
reexecução (`SanitizationRerun`, linhas 5 a 8): o `Preserve` de lugar, que os itens anteriores à
fatia A guardam, vira `Pseudonymize` sem chamar o serviço, pela
[0056](../../../decisoes/0056-toponimo-vira-marcador.md).

## Critérios de aceite

| Parte | Testes |
|---|---|
| F1 | CoreferenceMeasurementReportsPrecisionAndFusionApart; MeasurementCoversMessagesMixingLanguages; ModelBelowTheThresholdFailsTheMeasurement |
| F2 | EveryOccurrenceComesWithItsGroup; GroupPointingNowhereIsRefused; ResponseWithoutGroupsIsRefusedByTheNewContract; FakeServiceGivesGroups |
| F3 | MentionsOfOneGroupShareTheMarker; DistinctGroupsWithTheSameNameGetDistinctMarkers (#100 CA-2); PseudonymizedTextHasTheMarkersAndNoSeededOriginal (#102 CA-1); BatchRejectedForAnalysisLeavesNoPseudonymizedText (#102 CA-5); TwoBatchesOfOneDomainAtOnceGiveOneMarkerPerReferent; CancelledWhilePseudonymizingLeavesNoItemWithoutItsMap; AuthorGetsAMarkerStoredWithTheItem; ResanitizedItemIsPseudonymizedAgainInTheSameTransaction; ItemWithoutPseudonymizedGetsItsFirstOne; PreservedPlaceOfAnOldItemBecomesAMarker; OldItemWithAnOccurrenceWithoutPairIsRefusedWithTheReason |

A F3 fecha a #102: os CA-1 e CA-5 só se provam pela submissão.

## Peças, raia e dependências

| Peça | Arquivos | Padrão |
|---|---|---|
| Medição e decisão (F1) | o procedimento 015, uma ferramenta em `language-service/tools`, a decisão nova | — |
| Contrato e serviço (F2) | `language-service/src/language_service/recognizers`, `Core.Boundary.Contracts`, `ResponseValidator` | [002](../../padroes/002-servico-de-dominio.md) |
| Núcleo (F3) | `DecidedOccurrence`, a troca da fatia B, `SubmissionAnalysis`, `RerunService` | [002](../../padroes/002-servico-de-dominio.md), [001](../../padroes/001-armazenamento.md) |

- **Riscos:** precisão em pt-BR, mistura de línguas e fusão de pessoas distintas, em [riscos](riscos.md).
- **Raia:** o serviço de linguagem e a fronteira (F1 e F2); a submissão e a reexecução (F3).
- **Depende de:** F1 e F2, da fatia A; F3, de F2 e da fatia B. **Bloqueia:** a fatia E e o fim
  da fase.
