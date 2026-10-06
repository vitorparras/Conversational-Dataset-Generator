# 005 — A sanitização preserva o sinal do material

> Norma vigente. 2026-10-04.

**O texto que chega à anotação conserva as marcas linguísticas do original: registro, grafia,
pontuação expressiva e variação de uso.**

## Por que existe

A [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) estabelece que o dataset é contribuição
e que o que o prejudica não entra. Uma sanitização que corrige erro de grafia, normaliza
maiúsculas, desfaz gíria ou descarta emoji produz corpus limpo e inútil: o objeto de estudo é
exatamente como se escreve, e não a versão arrumada do que se escreveu.

Há um segundo motivo, menos óbvio. O que distingue um autor de outro é esse sinal. Apagá-lo apaga
também aquilo que a anotação observa, e o resultado é um conjunto de textos indistinguíveis entre
si onde deveria haver vozes diferentes.

## Critérios de aceitação

- Dado um texto com erro de grafia, gíria, repetição de letra ou pontuação expressiva, quando ele é
  sanitizado, então essas marcas continuam no resultado.
- Dado um texto com emoji e símbolos, quando ele é sanitizado, então eles permanecem, salvo quando
  identificam pessoa.
- Dado um texto que chegou com caracteres trocados por erro de codificação, quando o reparo é
  aplicado, então ele corrige a codificação sem reescrever palavra alguma.
- Dado um passo qualquer de sanitização, quando ele altera o texto, então o efeito fica restrito ao
  que esse passo declara tratar, e a alteração é atribuível a ele.

## O que não exige

Não exige preservar o que a política manda remover: quando esta garantia e a de
[004](004-sanitizacao-sem-residuo.md) se encontram no mesmo trecho, a remoção ganha, e a perda de
sinal é consequência aceita. Não exige preservar formatação de origem — negrito, cor, disposição na
página —, que não é sinal linguístico.
