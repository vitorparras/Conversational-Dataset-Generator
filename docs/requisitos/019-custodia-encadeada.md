# 019 — A cadeia de custódia é imutável e encadeada

> Norma vigente. 2026-10-04.

**Todo evento sobre o acervo fica registrado de modo que alterar ou remover qualquer evento passado
seja detectável, e dois eventos gravados ao mesmo tempo produzem cadeia válida ou falham
visivelmente.**

## Por que existe

A [decisão 0002](../decisoes/0002-stack.md) escolhe registro que não admite alteração e o
encadeamento entre registros, e nomeia o caso em que isso quebra: duas gravações simultâneas
corrompendo a cadeia em silêncio. O silêncio é o problema. Cadeia quebrada que se anuncia é um
achado; cadeia quebrada que continua aceitando eventos é um acervo cuja integridade ninguém pode
mais afirmar.

Registro que o próprio sistema pode reescrever não é registro. É por isso que a imutabilidade não
pode depender de a aplicação lembrar de não apagar.

## Critérios de aceitação

- Dado um evento gravado, quando alguém tenta alterá-lo ou removê-lo, então a operação falha, e a
  tentativa fica registrada.
- Dado dois eventos gravados ao mesmo tempo, quando ambos são aceitos, então a cadeia resultante é
  válida; se não puder ser, nenhuma das gravações é aceita em silêncio.
- Dado um evento no meio da cadeia, quando ele é removido ou substituído por fora do sistema, então
  a quebra do vínculo com o anterior é detectável.
- Dado o próprio sistema agindo sobre o acervo, quando ele age, então a ação é registrada pelo mesmo
  mecanismo: não há ator isento.

## O que não exige

Não exige guardar no registro o conteúdo do que foi lido ou alterado — o que fica fora dele está em
[021](021-registro-sem-dado-pessoal.md), e o material em si em
[002](002-origem-preservada-e-rastreavel.md). Não exige cadeia única para a instalação inteira: pode
haver uma por domínio, desde que cada uma seja íntegra e verificável por
[020](020-integridade-verificavel.md).
