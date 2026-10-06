# 017 — O acesso é negado por padrão

> Norma vigente. 2026-10-04.

**Pessoa alguma alcança material, anotação ou métrica sem concessão explícita de papel e de domínio,
e toda concessão ou revogação fica registrada.**

## Por que existe

A [decisão 0002](../decisoes/0002-stack.md) escolhe autorização avaliada antes do código que atende
ao pedido, em vez de condicionais espalhadas, e a
[decisão 0006](../decisoes/0006-dominios-coexistem.md) acrescenta o domínio como eixo de acesso.

A razão de o padrão ser negar é prática: num sistema que libera por omissão, a próxima operação
escrita nasce aberta, e ninguém percebe até que alguém alcance o que não devia. Negar por omissão
troca vazamento silencioso por erro visível no primeiro uso legítimo.

## Critérios de aceitação

- Dado uma pessoa sem concessão para um domínio, quando ela pede qualquer item desse domínio, então
  o pedido é negado, e a negativa não revela se o item existe.
- Dado uma operação nova que nenhuma política cobre, quando ela é chamada, então ela nega.
- Dado uma concessão, revogação ou mudança de papel, quando acontece, então fica registrada com quem
  fez, sobre quem, quando e com que escopo.
- Dado uma pessoa cujo papel é anotar, quando ela pede reversão de pseudonimização ou exportação,
  então é negado pela política, e não por a tela estar escondida.

A prova é um teste que percorre todas as operações expostas e falha quando alguma atende a um pedido
sem concessão correspondente, previsto em testes/acesso/negacao-por-padrao e ainda inexistente.

## O que não exige

Não exige um conjunto fixo de papéis, que é escolha de política. Não exige segundo fator de
autenticação. Não exige que a negativa explique o que falta — explicar demais informa quem tenta.
