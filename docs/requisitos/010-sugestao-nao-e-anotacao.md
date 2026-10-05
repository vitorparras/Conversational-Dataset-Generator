# 010 — Sugestão automática não é anotação

> Norma vigente. 2026-10-04.

**O que a máquina sugere nunca entra no dataset como decisão de pessoa, e cada rótulo exportado diz
como foi produzido.**

## Por que existe

A anotação deste sistema é assistida, e assistência acelera e enviesa pelo mesmo mecanismo: diante
de uma sugestão plausível, concordar custa menos que examinar. Se sugestão e decisão se confundem na
gravação, a concordância entre pessoas deixa de medir pessoas e passa a medir quanto cada uma
aceitou a máquina — e o número sobe justamente quando a qualidade cai.

A [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) trata o dataset como contribuição
própria. Um dataset cuja anotação não se sabe de onde veio não serve de referência para ninguém.

## Critérios de aceitação

- Dado uma sugestão exibida, quando a pessoa não age sobre ela, então nada é gravado como anotação.
- Dado um rótulo gravado, quando se pergunta sua origem, então a resposta distingue sugestão aceita
  sem alteração, sugestão corrigida e rótulo criado pela pessoa.
- Dado um conjunto de itens, quando a concordância é calculada, então ela é calculável também
  desconsiderando os rótulos que vieram de sugestão aceita sem alteração.
- Dado que o mecanismo de sugestão mude, quando ele muda, então as anotações já gravadas permanecem
  como estavam, e as que foram sugeridas por ele continuam identificáveis.

## O que não exige

Não exige recusar a assistência, que existe porque anotar à mão não escala. Não exige que a taxa de
aceitação seja baixa — exige que ela seja visível, para que quem avalia o dataset decida o que fazer
com ela. Não exige registrar o que foi sugerido e descartado.
