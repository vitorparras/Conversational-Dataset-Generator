# 020 — A integridade da custódia é verificável sob demanda

> Norma vigente. 2026-10-04.

**A cadeia inteira pode ser verificada a qualquer momento, e a verificação aponta o primeiro ponto
em que ela quebra.**

## Por que existe

Encadeamento sem verificação é promessa que ninguém cobra. O que torna
[019](019-custodia-encadeada.md) uma garantia, e não uma intenção, é alguém poder percorrer a cadeia
e dizer, com data, que ela estava íntegra. A pasta de [operação](../operacao/README.md) diz por quê:
verificação que existe mas nunca roda não é garantia.

Apontar onde quebrou é parte da garantia, não conforto. Saber que há uma quebra em algum lugar do
acervo não permite agir; saber qual é o primeiro evento inconsistente delimita o que foi afetado e o
que não foi.

## Critérios de aceitação

- Dado um acervo íntegro, quando a verificação roda, então ela conclui e declara o que verificou:
  escopo, quantidade de eventos e instante.
- Dado um evento alterado no armazenamento por fora do sistema, quando a verificação roda, então ela
  falha e identifica o primeiro evento inconsistente.
- Dado uma verificação concluída, quando se pergunta quando a última rodou e com que resultado,
  então a resposta está registrada e é consultável.
- Dado uma quebra detectada, quando a exportação é pedida sobre o escopo afetado, então ela é
  bloqueada.

## O que não exige

Não exige verificação contínua, nem periodicidade fixa: quando e por quem ela roda é assunto de
operação. Não exige que a verificação conserte a quebra — não há conserto. Quebra de custódia é
achado a registrar e investigar, e um mecanismo que a silenciasse valeria menos que nenhum.
