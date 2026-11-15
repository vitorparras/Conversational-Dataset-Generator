# Fatia E — reexecução sobre o bruto, pela tela

> Norma pendente. 2026-11-15. Fecha #97. Teto da fatia: 1.200 linhas de produção.

A Curadoria abre `/sanitizacao` e vê, por versão da lista de passos, quantos itens de lotes aceitos
estão nela. Com uma versão nova, ela confirma a reexecução numa página própria, como no
esvaziamento (`EmptyingScreensEndpoint`, linhas 26 e 27), e vê o resultado: idênticos,
substituídos e recusados, cada recusa com o motivo. Interrompida, a tela mostra o que falta e
oferece continuar. O protótipo é `reexecucao.html`, em [protótipos](prototipos/index.html).

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` | RerunOfTheSameVersionGivesTheIdenticalText (#97 CA-1, propriedade) |
| `Core.Persistence.Content.Tests` | RerunWithTheSameVersionIsReportedIdenticalAndNotRewritten (#97 CA-1); RerunOfAnEmptiedItemIsRefusedWithTheReason (#97 CA-2); RerunOfAnItemWithLostKeyIsRefusedWithTheReason (#97 CA-2); NewVersionReplacesTheTextAndRecordsTheVersion (#97 CA-3); InterruptedRerunLeavesNoItemWithTextOfOneVersionAndRecordOfAnother (#97 CA-4); RerunEventRecordsWhatWithWhichVersionAndResult (#97 CA-5, 0064); ResanitizingWithoutTheEventIsRefused (0064); TwoRerunsAtOnceTreatEachItemOnce |
| `Core.Host.Tests` | RerunScreenShowsItemsByVersion; NothingToRerunSaysSoAndOffersNoButton; RerunIsConfirmedOnItsOwnPage; ConfirmedRerunRedirectsToTheResult; InterruptedRerunShowsWhatIsLeftAndOffersToContinue; DatabaseLostWhileRerunningKeepsTheItemsAlreadyTreated; ReviewAfterRerunShowsTheNewVersion; RerunWithoutCurationAnswersNotFound |
| `Core.Host.Tests.Browser` | RerunIsConfirmedByKeyboardAndPassesTheAccessibilityCheck |

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Migração | `0064` em `core/src/Core.Persistence/Migrations/Sql` | [001](../../padroes/001-armazenamento.md) |
| Reexecução | SanitizationRerun (novo), sobre a lista de passos da fatia B | [002](../../padroes/002-servico-de-dominio.md) |
| Leitura e troca | um armazenamento novo, ao lado de `ContentWriter` | [001](../../padroes/001-armazenamento.md) |
| Rotas | um endpoint novo, como `EmptyingScreensEndpoint`, e `ScreenPaths` | [003](../../padroes/003-rota.md) |
| Tela | a tela da reexecução, com os estados do protótipo | [004](../../padroes/004-tela.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md), [006](../../padroes/006-teste-de-navegador.md) |

## Riscos, raia e dependências

- **Riscos:** duas reexecuções ao mesmo tempo, revisão durante a reexecução, queda no meio, banco
  caído e item esvaziado, nas linhas da fatia E em [riscos](riscos.md).
- **Raia:** a reexecução, o armazenamento dela, a rota e a tela nova. Lê a lista de passos e não
  a altera; não toca a submissão.
- **Depende de:** a fatia B. **Bloqueia:** nada; roda em paralelo com C e D.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
