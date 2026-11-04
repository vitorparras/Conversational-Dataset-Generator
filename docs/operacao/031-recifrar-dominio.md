# 031 — Recifrar um domínio

> Operacional. 2026-11-04.

Depois de rotacionar o rótulo de um domínio ([003](003-rotacao-da-chave.md)), cada linha segue com
a chave embrulhada sob o rótulo de quando foi gravada. Recifrar é embrulhar essa chave de novo sob
a geração mais nova não revogada. O conteúdo cifrado e a época não são tocados.

## O comando

`./Core.Host rewrap-domain --domain <id>` roda uma vez, com a credencial de dono
([010](010-credencial-de-dono-do-banco.md)), a configuração de `migrate`
([016](016-migracao-do-esquema.md)) e os três caminhos `MasterKey:` de
[024](024-arquivos-de-custodia-da-chave.md). O arquivo de rótulos traz a geração nova, registrada
no banco por `rotate-domain-label`. Outros domínios e linhas esvaziadas ficam como estão.

Os lotes são de 500 linhas, cada um numa transação com um evento seu na cadeia (`domain_rewrapped`),
que o banco exige na mesma transação
([decisão 0050](../decisoes/0050-banco-amarra-a-mudanca-do-conteudo-a-um-evento.md)).
Se o comando parar no meio, o que um lote gravou fica e o que não chegou a gravar não deixa nada.
Repita: ele só toca as linhas ainda atrás e, sem nenhuma, não grava lote nem evento.

Com `--status` só lê e diz quantas chaves já estão na geração mais nova não revogada e quantas
faltam. Faltando zero, o domínio está todo recifrado.

## Códigos de saída

- **0**: feito, ou contagem dada.
- **39**: recusa, como chave que não abre ou cadeia que não aceitou o evento; lotes gravados ficam.
- **40**: o banco não respondeu; os lotes gravados ficam.
- **78**: faltou `--domain` ou um dos caminhos.

A saída diz só contagens, nunca uma chave, uma época ou um rótulo.
