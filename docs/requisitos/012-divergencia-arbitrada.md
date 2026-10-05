# 012 — Divergência é arbitrada, e a resolução é rastreável

> Norma vigente. 2026-10-04.

**Duas anotações divergentes do mesmo item são resolvidas por uma terceira pessoa, e a resolução
registra o que foi escolhido, por quem e contra o quê.**

## Por que existe

A divergência é o achado mais informativo do processo: ela aponta onde a definição de uma categoria
é ambígua. Resolver sem registrar apaga exatamente essa informação, e o que sobra é um dataset
consensual por fora cuja ambiguidade ninguém mais consegue localizar.

Registrar contra o quê a escolha foi feita é o que permite corrigir a taxonomia em vez de atribuir o
desacordo a desatenção de quem anotou — e a taxonomia é configuração versionada justamente para
poder ser corrigida, conforme a
[decisão 0004](../decisoes/0004-taxonomia-configuravel.md).

## Critérios de aceitação

- Dado duas anotações divergentes, quando o item é fechado, então existe uma resolução atribuída a
  uma pessoa que não é nenhuma das duas.
- Dado uma resolução gravada, quando o item é consultado, então as anotações originais continuam
  disponíveis: resolver não apaga o que foi resolvido.
- Dado uma resolução gravada, quando a concordância é recalculada, então ela usa as anotações
  originais, nunca a resolvida — senão a medida passa a descrever a arbitragem.
- Dado um item com divergência não resolvida, quando a exportação é pedida, então ele sai marcado
  como não resolvido ou fica fora do recorte, e nunca sai como se houvesse consenso.

## O que não exige

Não exige que a arbitragem caiba sempre à mesma pessoa, nem que ela justifique por escrito cada
escolha — o registro mínimo é quem, quando, o que foi escolhido e o que foi recusado. Não exige que
a divergência seja rara: divergência frequente é informação, não falha de processo.
