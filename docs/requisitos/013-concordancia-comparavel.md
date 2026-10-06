# 013 — A concordância é medida sobre base declarada e comparável

> Norma vigente. 2026-10-04.

**Toda medida de concordância declara sobre o que foi calculada, é reproduzível a partir dos mesmos
dados, e permite comparar um domínio com outro.**

## Por que existe

A [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) coloca o método entre as contribuições
do projeto, e a [decisão 0006](../decisoes/0006-dominios-coexistem.md) mantém domínios na mesma
instalação porque a demonstração de que o método se transfere é uma comparação entre eles.

Comparação exige base explícita. Um coeficiente sem dizer sobre quais itens, com quantas pessoas e
sobre qual unidade de análise foi calculado não é verificável nem comparável — e dois números
calculados sobre bases diferentes parecem comparáveis, o que é pior que não ter número.

## Critérios de aceitação

- Dado uma medida de concordância, quando ela é exibida ou exportada, então declara o domínio, a
  taxonomia com versão, a unidade de análise, os itens considerados e quantas pessoas anotaram cada
  um.
- Dado o mesmo conjunto de anotações, quando a medida é recalculada, então o resultado é o mesmo.
- Dado dois domínios medidos, quando suas medidas são comparadas, então a comparação usa a mesma
  definição de unidade, e o sistema diz o que a impede quando algo a impede.
- Dado itens com uma anotação só, quando a medida é calculada, então eles ficam fora dela e são
  reportados à parte, em vez de entrarem como concordância perfeita.

## O que não exige

Não exige um coeficiente específico: qual medida se usa é escolha de método, e esta garantia vale
para qualquer uma. Não exige piso de concordância para exportar — a exportação declara o que mediu,
conforme [014](014-exportacao-declara-o-recorte.md), e quem recebe julga.
