# 031 — Recifrar um domínio

> Operacional. 2026-11-04.

Depois de rotacionar o rótulo de um domínio ([003](003-rotacao-da-chave.md)), cada linha segue com
a chave embrulhada sob o rótulo de quando foi gravada. Recifrar embrulha essa chave de novo sob a
geração mais nova não revogada, sem tocar no conteúdo cifrado, na época nem em outros domínios.

`./Core.Host rewrap-domain --domain <id>` roda uma vez, com a credencial de dono
([010](010-credencial-de-dono-do-banco.md)), a configuração de `migrate`
([016](016-migracao-do-esquema.md)) e os três caminhos `MasterKey:` de
[024](024-arquivos-de-custodia-da-chave.md); o arquivo de rótulos traz a geração nova.
Os lotes são de 500 linhas, cada um numa transação com seu evento `DomainRewrapped` na cadeia
([decisão 0050](../decisoes/0050-banco-amarra-a-mudanca-do-conteudo-a-um-evento.md)). Se o comando
parar, os lotes gravados ficam; repita, e ele só toca as linhas ainda atrás.
Com `--status` só lê e diz quantas chaves já estão na geração mais nova e quantas faltam; zero
faltando é domínio todo recifrado.

Saída: **0** feito; **39** recusa (chave que não abre, rótulo novo fora do arquivo, cadeia que não
aceitou o evento); **40** banco sem resposta; **78** faltou `--domain` ou um caminho.
