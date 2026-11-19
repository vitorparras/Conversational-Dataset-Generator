# Fatia D — identificador partido e sem resíduo

> Norma pendente. 2026-11-15. Fecha #91 e #94. Teto da fatia: 900 linhas de produção.

Um passo entra na lista, antes da aplicação dos destinos: a remoção do identificador estruturado,
inclusive quando partido por caractere invisível ou separador incomum. Ele produz [documento] e
[contato], pela tabela da fatia B dos [contratos](contratos.md). A propriedade de que não
sobra resíduo passa a valer para a lista inteira, e é medida com um defeito plantado. O protótipo é
`lote.html`, em [protótipos](prototipos/index.html).

## Critérios de aceite

Cada nome é um teste; as propriedades levam `[Trait("Kind", "Property")]`.

| Projeto | Testes |
|---|---|
| `Core.Ingestion.Tests` (#91) | DocumentNumberWithUsualPunctuationLeaves (CA-1); NumberSplitByZeroWidthOrOddSeparatorLeavesWithTheSeparators (CA-2); MoneyQuantityAndDateStay (CA-3); TextWithoutIdentifierLeavesUnchanged (CA-4); SpanMarkedAndRecognizedLeavesOnceWithoutNestedMarker (CA-5) |
| `Core.Ingestion.Tests` (#94) | ClosedFormIdentifierLeavesNoRecognizableVariant (CA-1, propriedade); IdentifierSplitByInvisibleCharacterLeaves (CA-2, propriedade); SanitizingTwiceEqualsOnce (CA-3, propriedade); TextWithNothingToRemoveLeavesIdentical (CA-4, propriedade); ResidueCheckRejectsARemovalStepWithAPlantedDefect (CA-5) |
| `Core.Host.Tests` | BatchScreenShowsTheIdentifierCount |

A #94 CA-5 também vale na mutação do PR: o job mutation_module
(`.github/workflows/verify-code.yml`, linha 361) roda sobre o módulo que o diff toca, e a
fatia confere que o módulo dos passos entra no alcance.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Passo | um arquivo novo na pasta Sanitizing | [002](../../padroes/002-servico-de-dominio.md) |
| Lista | a lista de passos, uma linha | [002](../../padroes/002-servico-de-dominio.md) |
| Geradores | os identificadores sintéticos, em forma fechada, para o `PropertyRunner` | [002](../../padroes/002-servico-de-dominio.md) |
| Mutação | o alcance da mutação, em `tools/verify-code/steps` | [002](../../padroes/002-servico-de-dominio.md) |
| Tela do lote | o rótulo da contagem nova na I3 | [004](../../padroes/004-tela.md) |

## Riscos, raia e dependências

- **Riscos:** resíduo de identificador partido, contido pela propriedade da #94 e pelo defeito
  plantado; os geradores usam só identificadores sintéticos, nunca um número real.
- **Raia:** o passo, os geradores e a mutação. Divide com a C só a lista de passos.
- **Depende de:** a fatia B. **Bloqueia:** nada; a C roda em paralelo.

Ver [contratos](contratos.md) e [riscos](riscos.md).
