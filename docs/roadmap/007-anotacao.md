# Fase 7 — Anotação

> Norma vigente. 2026-10-04.

Duas coisas entram juntas, e é deliberado. A taxonomia, que a decisão
[0004](../decisoes/0004-taxonomia-configuravel.md) define como arquivo versionado carregado na
inicialização — identificador estável, rótulo, definição e exemplos do que inclui e do que não
inclui, por domínio. E a tela onde o trabalho humano acontece, que é a primeira consumidora dela.

Entra também a assistência. O requisito
[010](../requisitos/010-sugestao-nao-e-anotacao.md) não a proíbe: exige que sugestão e decisão de
pessoa nunca se confundam na gravação, e que a origem de cada rótulo seja recuperável depois. Vale
construir as duas coisas ao mesmo tempo, porque acrescentar procedência a anotações já gravadas é
impossível — ninguém sabe mais de onde elas vieram.

A tela respeita as quatro formas da decisão [0005](../decisoes/0005-unidades-de-analise.md). Anotar
conversa recai sobre mensagem; anotar sequência rotulada recai sobre trecho; anotar documento recai
sobre o todo. Misturar isso tornaria as três imprecisas de uma vez.

## O que precisa estar pronto antes

A [fase 6](006-acesso.md) e a [fase 5](005-pseudonimizacao.md). Também o eixo de domínio da
[fase 2](002-custodia-e-cifra.md): a taxonomia é por domínio, e não há onde carregá-la antes disso.

Há um pré-requisito que não é código. A decisão
[0001](../decisoes/0001-as-duas-contribuicoes.md) reabre se nenhum segundo domínio aparecer até o
início da anotação. Identificar esse domínio é condição para começar aqui, mesmo que seu material só
atravesse o sistema na [fase 10](010-segundo-dominio.md).

## Critério de término

1. [008](../requisitos/008-rotulo-dentro-da-taxonomia.md) é verificado nas duas pontas: o sistema
   não sobe com taxonomia inválida, e rótulo fora dela é recusado na escrita.
2. Uma instalação com taxonomia inteiramente diferente passa nas mesmas validações — a prova de que
   nenhuma categoria virou tipo no código nem lista percorrida por validação.
3. [009](../requisitos/009-anotacao-de-trecho-estavel.md) é verificado, inclusive a falha visível:
   anotação que não resolve nunca resolve para o trecho vizinho.
4. [010](../requisitos/010-sugestao-nao-e-anotacao.md) e
   [011](../requisitos/011-anotacoes-independentes.md) são verificados antes de a anotação de
   produção começar — depois, o dado já está contaminado.
5. Cada uma das quatro formas tem sua anotação, e toda anotação registra sobre o que recaiu e sob
   qual versão da taxonomia foi feita.

## O que destrava

A medição de concordância, e com uma base que significa algo: divergência observada passa a ser
divergência de julgamento, não de digitação nem de quem viu o rótulo alheio.
