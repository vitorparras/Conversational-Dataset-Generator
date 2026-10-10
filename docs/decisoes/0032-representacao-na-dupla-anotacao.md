# 0032 — Representação das categorias na dupla anotação

> Decisão vigente. 2026-10-07.

## Contexto

Parte dos itens é anotada por duas pessoas, para que a concordância seja medida, e essa fração é
sorteada ao acaso do mesmo conjunto.

A [decisão 0027](0027-tratamento-uniforme-das-categorias.md) exige que todas as categorias recebam o
mesmo tratamento. Lidas juntas, as duas coisas pedem que a dupla anotação represente todas as
categorias — e **isso é impossível no momento do sorteio**: a categoria de um item nasce da
anotação, então quando o sorteio acontece ela ainda não existe.

Sortear sabendo a categoria exigiria conhecer a resposta antes de medir, o que destrói a própria
medida.

## Decisão

**O sorteio é simples e ao acaso**, sem olhar categoria — porque não há categoria para olhar.

**Depois da anotação, a métrica relata quantos itens de cada categoria caíram na dupla anotação.**
O relatório acompanha a medida de concordância, sempre, e não é opcional.

**Onde a representação for baixa, a medida daquela categoria se declara frágil** em vez de aparecer
com a mesma autoridade das demais. O limite abaixo do qual a medida é declarada frágil está na
configuração do domínio.

A concordância geral permanece válida: ela foi medida numa fração sorteada ao acaso, que é
exatamente o que a torna estendível ao resto.

## Recusados

**Segunda passagem que completa as categorias sub-representadas.** Tornaria a medida mais sólida por
categoria, e a segunda amostra não seria ao acaso — ela escolhe sabendo o rótulo. Misturar as duas
num número só produz medida enviesada sem avisar.

**Piso de itens por categoria antes de reportar.** Coerente com recusar em vez de avisar, e deixaria
categoria rara sem medida para sempre, travando a prontidão do dataset por algo que o sorteio ao
acaso não pode resolver.

## Consequências

Categoria rara tem medida frágil, e isso fica escrito em vez de implícito. Quem usa o dataset sabe
em quais categorias confiar e em quais não — o que é mais honesto que um número uniforme que esconde
a diferença.

A fragilidade é consequência do material, não defeito do instrumento: categoria que aparece pouco
aparece pouco na amostra. Aumentar a fração sob dupla anotação ajuda, e tem custo de trabalho
humano.

## O que reabre

Se uma categoria importante permanecer frágil por campanhas sucessivas, a amostragem estratificada
volta à mesa — com a exigência de ser medida e reportada **à parte** da concordância sorteada, nunca
somada a ela.
