# Fase 4 — Sanitização

> Norma vigente. 2026-10-04.

A sanitização tem duas obrigações que apontam para lados opostos, e é a tensão entre elas que a
torna difícil: [004](../requisitos/004-sanitizacao-sem-residuo.md) manda que nada que deveria sair
sobreviva, e [005](../requisitos/005-sanitizacao-preserva-o-sinal.md) manda que o sinal linguístico
do original permaneça. O requisito 005 já diz quem ganha quando as duas se encontram no mesmo
trecho; esta fase é onde esse arbítrio deixa de ser texto e passa a ser código.

A segunda obrigação não é conforto. A decisão [0003](../decisoes/0003-cifra-em-repouso.md) justifica
manter o texto sanitizado em claro justamente porque ele é o material que as pessoas leem e filtram
durante a anotação — e texto achatado pela limpeza é texto que não serve para anotar.

Entram os passos de normalização, cada um com escopo estrito e declarado, e a verificação dos dois
invariantes por teste baseado em propriedades sobre texto gerado. A decisão
[0007](../decisoes/0007-garantia-de-qualidade.md) é explícita: estes invariantes são afirmações
sobre todo texto possível, e exemplo escolhido à mão testa só os casos em que quem escreveu pensou.

Entra também a reexecução. O bruto permanece intacto e cifrado, e a sanitização é reexecutável sobre
ele: um passo corrigido não obriga a reingerir o acervo.

## O que precisa estar pronto antes

A [fase 3](003-ingestao.md), de onde vem material com forma já validada. E a
[fase 1](001-fundacao-verificavel.md), pelo aparato de propriedade e mutação.

## Critério de término

1. [004](../requisitos/004-sanitizacao-sem-residuo.md) e
   [005](../requisitos/005-sanitizacao-preserva-o-sinal.md) são verificados por propriedade sobre
   texto gerado, não apenas por exemplo escolhido.
2. Cada passo declara seu escopo, e um teste mostra que ele não altera nada fora dele.
3. Reexecutar a sanitização sobre o mesmo bruto produz exatamente o mesmo resultado.
4. A saída da sanitização **não** ocupa ainda a coluna em claro com material real: enquanto a
   identidade não tiver saído do texto, a saída permanece cifrada.

## O que destrava

A pseudonimização passa a operar sobre texto estável. Posição de caractere calculada sobre o texto
final não se desloca depois — e é disso que
[009](../requisitos/009-anotacao-de-trecho-estavel.md) vai depender, duas fases adiante.
