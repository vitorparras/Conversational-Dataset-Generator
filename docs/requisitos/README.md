# Requisitos

> Descrição. O que entra nesta pasta.

**Um requisito é uma garantia observável do sistema, não uma tarefa.** Ele descreve o que o sistema
assegura e por quê; sobrevive a qualquer replanejamento. O trabalho para atingi-lo vive nas issues.

| Não é requisito | É requisito |
|---|---|
| "Implementar transação na gravação de auditoria" | "Dois eventos gravados ao mesmo tempo produzem cadeia válida, ou a gravação falha visivelmente" |
| "Criar tela de arbitragem" | "Duas anotações divergentes do mesmo item são resolvidas por uma terceira pessoa, e a resolução é rastreável" |

A coluna da esquerda cita solução; a da direita, garantia. **Requisito que cita tecnologia está
quase sempre errado** — tecnologia é escolha de decisão, e muda sem que a garantia mude.

## Índice

| # | Requisito |
|---|---|
| [001](001-forma-validada-na-ingestao.md) | A forma do material é validada na ingestão |
| [002](002-origem-preservada-e-rastreavel.md) | A origem do material é preservada e rastreável |
| [003](003-copia-do-armazenamento-nao-expoe.md) | Uma cópia do armazenamento não expõe nada |
| [004](004-sanitizacao-sem-residuo.md) | A sanitização não deixa resíduo |
| [005](005-sanitizacao-preserva-o-sinal.md) | A sanitização preserva o sinal do material |
| [006](006-pseudonimizacao-preserva-contexto.md) | A pseudonimização preserva o contexto |
| [007](007-reversao-restrita-e-auditada.md) | A reversão é restrita e auditada |
| [008](008-rotulo-dentro-da-taxonomia.md) | O rótulo gravado pertence à taxonomia vigente |
| [009](009-anotacao-de-trecho-estavel.md) | A anotação de trecho aponta para o mesmo trecho |
| [010](010-sugestao-nao-e-anotacao.md) | Sugestão automática não é anotação |
| [011](011-anotacoes-independentes.md) | Anotações independentes antes da comparação |
| [012](012-divergencia-arbitrada.md) | Divergência é arbitrada, e a resolução é rastreável |
| [013](013-concordancia-comparavel.md) | A concordância é medida sobre base declarada e comparável |
| [014](014-exportacao-declara-o-recorte.md) | A exportação declara o recorte de que saiu |
| [015](015-exportacao-sem-identidade.md) | A exportação não leva identidade |
| [016](016-metricas-recalculaveis.md) | As métricas do dataset são recalculáveis por quem o recebe |
| [017](017-acesso-negado-por-padrao.md) | O acesso é negado por padrão |
| [018](018-isolamento-entre-dominios.md) | Material de um domínio não aparece em consulta de outro |
| [019](019-custodia-encadeada.md) | A cadeia de custódia é imutável e encadeada |
| [020](020-integridade-verificavel.md) | A integridade da custódia é verificável sob demanda |
| [021](021-registro-sem-dado-pessoal.md) | O registro de operação não contém dado pessoal |
| [022](022-operacao-sem-servico-externo.md) | O sistema opera sem serviço externo |

## Formato

Nome `NNN-slug.md`. Autoridade `Norma vigente`. Seções:

1. **A garantia** — uma frase. Se não couber em uma, provavelmente são dois requisitos
2. **Por que existe** — a exigência de origem, citada
3. **Critérios de aceitação** — em Dado / Quando / Então, verificáveis
4. **O que não exige** — evita que a implementação assuma restrição que o requisito não impõe

Ver [regras de escrita](../regras-de-escrita.md).
