# 009 — A anotação de trecho aponta para o mesmo trecho

> Norma vigente. 2026-10-04.

**Toda anotação sobre um trecho resolve, depois, exatamente para o texto que foi anotado — ou falha
visivelmente, nunca para o trecho vizinho.**

## Por que existe

A [decisão 0005](../decisoes/0005-unidades-de-analise.md) cria a sequência rotulada, onde a anotação
recai sobre trechos e não sobre o todo. A [decisão 0002](../decisoes/0002-stack.md) registra o risco
em uma frase: um índice errado anonimiza o pedaço errado do texto.

Deslocamento é pior que perda. Quando um limite escorrega, os rótulos continuam todos lá, bem
formados, apontando para outra coisa — e um dataset assim não dá sinal de defeito algum. A única
defesa é tornar o deslocamento detectável no momento em que o trecho é recuperado.

## Critérios de aceitação

- Dado uma anotação de trecho, quando o item é exibido, exportado ou medido, então o texto
  recuperado pelos limites é o mesmo que estava ali quando a anotação foi feita.
- Dado um reprocessamento que altere o texto de um item, quando ele acontece, então as anotações de
  trecho afetadas ficam marcadas como desatualizadas, e não são reapontadas por aproximação.
- Dado limites que caiam fora do texto ou se cruzem de forma inválida, quando a anotação é gravada,
  então a gravação é recusada.
- Dado texto com caracteres fora do alfabeto latino básico, quando os limites são calculados, então
  a mesma convenção de contagem vale na gravação, na exibição e na exportação.

## O que não exige

Não exige que trechos anotados não se sobreponham entre si: sobreposição pode ser legítima e é
assunto de método. Não exige reconciliar automaticamente anotação desatualizada depois de um
reprocessamento — marcar basta, e refazer é decisão de quem anota.
