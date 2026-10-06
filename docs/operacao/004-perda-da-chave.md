# 004 — Perda da chave, e a recuperação que precede o dado

> Operacional. 2026-10-04.

Três perdas diferentes andam sob o mesmo nome, e confundi-las custa caro.

| O que se perdeu | O que é |
|---|---|
| A cópia de guarda, que pode estar com alguém | Incidente de exposição |
| Um rótulo de derivação | Dado ilegível com a chave em mãos |
| A chave, sem cópia utilizável | Perda definitiva do texto bruto e da reversão |

## A recuperação se testa antes de haver dado

A [decisão 0014](../decisoes/0014-custodia-da-chave-mestra.md) exige recuperação testada antes do
primeiro dado real: restaurar banco e chave num ambiente limpo e conferir que o texto bruto decifra
e que o mapa de reversão abre. Enquanto isso não tiver sido feito uma vez, ingestão de material real
não começa.

Repita a cada troca de quem detém a cópia, e a cada mudança no formato de persistência. É trabalho
recorrente e sem resultado visível quando dá certo — e é a única diferença entre recuperação e
expectativa.

## Perdeu-se a cópia de guarda

Trate como suspeita de vazamento ([008](008-incidente-vazamento.md)): alguém pode estar com ela.
Rotacione ([003](003-rotacao-da-chave.md)), estabeleça cópia nova com a custódia declarada outra
vez, e registre a antiga como comprometida. Se não se souber onde ela foi parar, a suspeita não
prescreve — fica registrada.

## Perdeu-se um rótulo

A chave abre, e o dado não. Procure o rótulo no que sobrou: cópia anterior da guarda, registro de
criação do domínio, e o próprio dado, que carrega o rótulo em claro — é para isso que ele é gravado
assim. Se nenhum caminho o devolver, o material cifrado sob ele caiu na perda definitiva abaixo, e
só ele.

## Não há cópia utilizável

O que se perdeu, em definitivo: o texto bruto anterior à sanitização e o mapa de reversão. Nada
decifra isso depois.

O que permanece, porque está em claro no banco pela
[decisão 0003](../decisoes/0003-cifra-em-repouso.md): texto sanitizado, anotações, métricas e
registro de eventos — a maior parte do que a anotação e a exportação consomem.

A decisão 0014 é direta sobre o resto: perda da chave é perda de acervo, e se registra como tal; não
se finge que o material existe porque as linhas continuam no banco. Na prática:

1. Registre a perda na custódia, com data, escopo e o que foi tentado
2. Declare o mapa de reversão inexistente — nenhum pedido de reversão será atendido, e dizer isso é
   melhor que deixar o pedido numa fila que nunca anda
3. Marque o material cujo bruto se perdeu: ele não pode mais ser ressanitizado se a política de
   sanitização mudar, e quem usar o dataset precisa saber
4. Estabeleça chave e cópia de guarda novas antes de ingerir qualquer coisa, e declare na exportação
   o que ficou sem bruto
