# Fase 5 — Pseudonimização

> Norma vigente. 2026-10-04.

A identidade sai do texto e vai para um mapa cifrado, com chave e finalidade próprias. É a fase que
fecha o raciocínio da decisão [0003](../decisoes/0003-cifra-em-repouso.md): o que torna o texto
sanitizado sensível é a ligação com a identidade real, e essa ligação passa a existir num único
lugar — cifrado, e cujo acesso é auditado.

Entram a substituição por rótulo estável, o mapa de reversão cifrado e a reversão como operação
restrita. **O reconhecimento das ocorrências não entra aqui:** ele acontece na entrada, sobre o
texto como chegou, e esta fase aplica o que foi decidido lá — é o que a
[arquitetura do módulo](../arquitetura/007-modulo-pseudonimizacao.md) e a
[política](../privacidade/001-dado-pessoal.md) estabelecem. O requisito
[006](../requisitos/006-pseudonimizacao-preserva-contexto.md) é o mais exigente da fase: não basta
ocultar, o material tem de continuar legível depois de oculto, porque é ele que será anotado.

Entra também o que atravessa a fronteira de processo: texto sensível vai do núcleo ao serviço de
linguagem, e o formato está fechado pela
[decisão 0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md) — o que trafega, o que
nunca entra em log, e o que fazer com resposta ausente ou malformada.

A unidade do deslocamento também está fechada: ponto de código, com a conversão no núcleo, que é o
lado que indexa diferente. Sem unidade única, a divergência cairia em emoji e caractere composto —
sem erro, só no lugar errado. Ver [fronteiras](../arquitetura/002-fronteiras.md).

## O que precisa estar pronto antes

A [fase 4](004-sanitizacao.md): posição de caractere calculada sobre texto que ainda vai mudar
aponta para o trecho errado depois da mudança. E a [fase 2](002-custodia-e-cifra.md), pela chave de
finalidade própria que o mapa exige.

## Critério de término

1. [006](../requisitos/006-pseudonimizacao-preserva-contexto.md) é verificado por propriedade:
   mesmo referente, mesmo rótulo; referentes distintos, rótulos distintos.
2. [007](../requisitos/007-reversao-restrita-e-auditada.md) é verificado, inclusive a parte que não
   se apaga — o registro da reversão sobrevive à tentativa de removê-lo.
3. O mapa de reversão está coberto por
   [003](../requisitos/003-copia-do-armazenamento-nao-expoe.md).
4. [021](../requisitos/021-registro-sem-dado-pessoal.md) continua valendo com o serviço de
   linguagem em uso, e isso é verificado nos dois processos — não só no núcleo.
5. A coluna em claro da decisão 0003 passa a receber material real, e o que entra nela já passou
   pela sanitização e pela pseudonimização, nessa ordem.

## O que destrava

A tela de anotação pode buscar e ordenar sobre texto em claro, que é o que torna o trabalho humano
viável. E o controle de acesso passa a ter a operação que mais precisa ser negada por papel: a
reversão.
