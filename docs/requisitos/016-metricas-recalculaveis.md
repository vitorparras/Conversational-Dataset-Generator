# 016 — As métricas do dataset são recalculáveis por quem o recebe

> Norma vigente. 2026-10-04.

**Quem recebe um dataset exportado consegue recalcular, com o que está no artefato, as métricas que
ele publica, e chega aos mesmos números.**

## Por que existe

A [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) trata o dataset e o método como
contribuições, e método que só o sistema de origem sabe executar não se transfere. Métrica não
recalculável é afirmação, não resultado: quem recebe só pode acreditar.

É também o que torna a comparação de [013](013-concordancia-comparavel.md) útil fora daqui. Dois
datasets com métricas recalculáveis podem ser comparados por terceiros; com métricas apenas
declaradas, não.

## Critérios de aceitação

- Dado um dataset exportado com suas métricas, quando alguém as recalcula a partir do que está no
  artefato, então chega aos mesmos valores.
- Dado o artefato, quando se procura a definição das categorias usadas, então ela está lá —
  identificador, rótulo, definição e exemplos do que inclui e do que não inclui.
- Dado itens com mais de uma anotação, quando o artefato é lido, então as anotações individuais são
  distinguíveis por anotador pseudonimizado, e a resolução de divergência vem marcada como tal.
- Dado uma métrica que o artefato não permita recalcular, quando a exportação roda, então ela não
  publica essa métrica.

## O que não exige

Não exige publicar identidade de anotador: anotador pseudonimizado basta para separar as anotações,
e a proteção segue valendo conforme [015](015-exportacao-sem-identidade.md). Não exige publicar
texto anterior à sanitização, nem o material integral quando o recorte exportado for parcial — exige
que o que saiu baste para as contas que saíram com ele.
