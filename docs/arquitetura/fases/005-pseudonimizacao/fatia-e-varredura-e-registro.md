# Fatia E — varredura das leituras e registro sem conteúdo

> Norma pendente. 2026-11-22. Fecha #106 e #107. Teto da fatia: 400 linhas de produção.

A fatia prova o que nenhum módulo prova sozinho: que o mapa é o único caminho de volta e que nenhum
registro leva o material. É quase toda teste. A produção que entra é o que a prova achar faltando:
um campo sem classificação, uma mensagem de erro que ecoa o texto, um campo do registro do serviço.

**Varredura (#106).** Material com originais sintéticos e improváveis entra pela submissão, passa
por todo o fluxo, e o teste chama cada leitura do inventário como quem não tem autorização de
reversão: telas, rotas, relatórios do lote, mensagens de erro, a página de endereço inexistente. Um
original achado reprova e nomeia a leitura. O inventário fica ao lado do teste e é conferido contra
as rotas que `ScreenAccessDeclarationTests` já lê: rota de conteúdo nova fora dele reprova.

**Registro (#107).** No núcleo, a redação já está no caminho do registro (`DataClassifications`). A
troca roda antes de o item existir, então o registro dela traz lote, posição do item, passo e
motivo, como o da sanitização na fase 4; depois de gravado, o id do item. No serviço, os campos são
fixos; a fatia confere tamanho e versão do modelo, que os testes de hoje não afirmam.

## Critérios de aceite

| Projeto | Testes |
|---|---|
| `Core.Host.Tests` (#106) | NoReadWithoutReversalReturnsASeededValue (CA-1); ReadPlantedToReturnTheSanitizedTextFailsTheSweep (CA-2); FailureMidProcessingShowsNoSeededValue (CA-3); ContentReadOutsideTheInventoryFailsTheCheck (CA-4) |
| `Core.Tests.Integration` (#107) | FullFlowLogsOfBothProcessesCarryNoSeededValue (CA-1) |
| `Core.Host.Tests` (#107) | ExceptionWhilePseudonymizingLogsBatchPositionStepAndReasonOnly (CA-2); UnmarkedTextPassedToTheLogIsRedactedAnyway (CA-3) |
| `language-service/tests/privacy` (#107) | test_served_request_logs_id_size_time_model_and_counts_without_offsets (CA-4); test_no_option_prints_the_request_body (CA-5) |

O CA-2 da #106 planta, só no teste, uma rota que devolve o sanitizado, e exige que a varredura a
reprove nomeando-a. O CA-3 da #107 é o caso em que quem escreve a chamada esquece de marcar o campo:
o teste passa o texto semeado sem classificação e exige a linha redigida. O CA-5 vai além de
`language-service/tests/unit/test_no_debug_mode.py`, que procura só depuração e recarga: procura
qualquer opção que leve o corpo ao registro.

## Peças e padrões

| Peça | Arquivos | Padrão |
|---|---|---|
| Varredura | o inventário e o teste, em `core/tests/Core.Host.Tests` | [005](../../padroes/005-teste-com-banco.md) |
| Registro | `DataClassifications`, a troca da fatia B, o serviço em `language-service` | [002](../../padroes/002-servico-de-dominio.md) |
| Testes | os nomes acima | [005](../../padroes/005-teste-com-banco.md) |

## Riscos, raia e dependências

- **Riscos:** leitura nova sem reversão e exceção que leva o texto ao log, nas linhas da fatia E em
  [riscos](riscos.md).
- **Raia:** testes e o que eles acharem; não cria tela nem tabela.
- **Depende de:** B, pela troca e pelo pseudonimizado, e D, pela leitura autorizada com que a
  varredura compara (#106, "Depende de"). **Bloqueia:** nada; fecha a fase.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
