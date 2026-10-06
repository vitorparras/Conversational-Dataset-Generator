# Fase 10 — O segundo domínio

> Norma vigente. 2026-10-04.

A decisão [0001](../decisoes/0001-as-duas-contribuicoes.md) fixa o critério: generalidade se
justifica por um segundo caso concreto, nunca por hipótese. Esta fase é o pagamento dessa conta.
Até ela, toda afirmação de que a ferramenta serve a outro domínio é promessa; depois dela é fato ou
é falso — e os dois resultados são úteis.

Entra material de um segundo domínio, com taxonomia própria, na mesma instalação, conforme a decisão
[0006](../decisoes/0006-dominios-coexistem.md), atravessando o pipeline inteiro. E entra a
comparação, que é a entrega real: concordância aqui e ali, comportamento da sanitização sobre
material cujas formas de dado pessoal são outras.

O que conta como validação já está escrito em `docs/pesquisa/006-validacao-em-outro-dominio.md`,
inclusive o que não conta — e as três armadilhas que ele lista são justamente as saídas fáceis desta
fase. A regra que a torna um teste de verdade: nada de código novo específico de domínio. Se for
preciso alterar código, a generalidade pretendida pelas decisões 0004 e 0006 não foi alcançada, e
esse achado vale mais que a fase.

## O que precisa estar pronto antes

Todas as fases anteriores. Em particular a [fase 9](009-exportacao.md): sem recorte declarado, dois
domínios na mesma base produzem exportações que ninguém separa depois.

A decisão sobre chave de cifra por domínio, que a decisão 0006 deixa para decisão própria **antes de
existir o segundo domínio real**, trava o início desta fase. Ver
[decisões pendentes](012-decisoes-pendentes.md).

## Critério de término

Os quatro significados de "funcionar" de `docs/pesquisa/006-validacao-em-outro-dominio.md` estão
verificados, e a medição descrita lá foi feita. Em particular:

1. O segundo domínio atravessa o pipeline inteiro sem alteração de código, e cada exceção a isso
   está registrada com o motivo.
2. A concordância sai nos dois domínios, nos mesmos escopos e com os mesmos denominadores.
3. A verificação de resíduo de [004](../requisitos/004-sanitizacao-sem-residuo.md) passa sobre
   material cujas formas de dado pessoal não foram as antecipadas.
4. [018](../requisitos/018-isolamento-entre-dominios.md) é verificado com os dois domínios
   povoados, e não mais com um só.
5. A condição de reabertura da decisão 0001 sobre a ausência de segundo domínio está respondida por
   fato, não por intenção.

## O que destrava

Nada mais neste roadmap: é a fase que fecha o método. O que vier depois é escopo novo, e
[fora de escopo](013-fora-de-escopo.md) registra o que já se sabe que virá.
