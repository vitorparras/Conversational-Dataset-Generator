# Requisitos

> Descrição. O que entra nesta pasta.

**Um requisito é uma garantia observável do sistema, não uma tarefa.** Ele descreve o que o sistema
assegura e por quê; sobrevive a qualquer replanejamento. O trabalho para atingi-lo vive nas issues.

| Não é requisito | É requisito |
|---|---|
| "Implementar transação na gravação de auditoria" | "Dois eventos gravados ao mesmo tempo produzem cadeia válida, ou a gravação falha visivelmente" |
| "Criar tela de arbitragem" | "Duas anotações divergentes do mesmo item são resolvidas por uma terceira pessoa, e a resolução é rastreável" |

A esquerda cita solução; a direita, garantia. **Requisito que cita tecnologia está quase sempre
errado** — tecnologia é escolha de decisão, e muda sem que a garantia mude.

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
| [023](023-atribuicao-por-pessoa.md) | A atribuição é por pessoa, e o item sabe quem falta |
| [024](024-marcacao-de-apoio-na-anotacao.md) | A marcação do que sustentou a decisão pertence à anotação |
| [025](025-bloco-e-recorte-de-leitura.md) | O bloco é recorte de leitura, não de significado |
| [026](026-material-inteiro-anotavel.md) | O material inteiro é anotável |
| [028](028-concessao-tem-prazo.md) | Concessão de acesso tem prazo |
| [029](029-metrica-sem-texto.md) | Avaliar qualidade não exige alcançar o texto |
| [030](030-exportacao-exige-duas-pessoas.md) | Exportar exige duas pessoas |
| [031](031-entrada-de-arquivo-contida.md) | A entrada de arquivo é contida |
| [032](032-configuracao-falha-fechada.md) | Configuração desconhecida impede a inicialização |
| [033](033-rascunho-de-anotacao-sobrevive.md) | O trabalho em andamento não se perde |
| [034](034-copia-vazada-e-atribuivel.md) | Cópia vazada é atribuível |
| [035](035-revisao-do-lote-por-amostra.md) | A revisão do lote é por amostra, e reprova inteiro |

## Formato

Nome `NNN-slug.md`. Autoridade `Norma vigente`. Seções: **a garantia** em uma frase (se não couber
em uma, são dois requisitos) · **por que existe**, com a exigência de origem citada · **critérios
de aceitação** em Dado / Quando / Então · **o que não exige**, que evita a implementação assumir
restrição que o requisito não impõe.

Ver [regras de escrita](../regras-de-escrita.md).
