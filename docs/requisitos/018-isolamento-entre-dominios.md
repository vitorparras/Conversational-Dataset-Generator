# 018 — Material de um domínio não aparece em consulta de outro

> Norma vigente. 2026-10-04.

**Material, anotação e métrica pertencem a exatamente um domínio, e consulta que não declare o
domínio não devolve nada, em vez de devolver tudo.**

## Por que existe

A [decisão 0006](../decisoes/0006-dominios-coexistem.md) aceita domínios na mesma instalação com uma
condição: o isolamento não pode depender de cada consulta estar correta. Basta um filtro esquecido
para vazar material entre domínios, e o filtro esquecido não dá sinal — devolve resultado a mais,
que parece resultado.

Por isso a garantia é formulada pelo pior caso: a consulta omissa devolve conjunto vazio. Vazio é
erro que aparece na primeira execução; resultado a mais é erro que aparece quando já é tarde.

## Critérios de aceitação

- Dado uma consulta que não declare domínio, quando ela é executada, então devolve conjunto vazio.
- Dado uma consulta que declare um domínio, quando ela é executada, então nenhum item de outro
  domínio aparece, ainda que a consulta não filtre nada explicitamente.
- Dado quem administra o armazenamento, quando consulta diretamente, então a mesma regra se aplica a
  essa pessoa.
- Dado uma métrica calculada, quando se pergunta a que domínio ela pertence, então a resposta é um
  domínio, ou um conjunto declarado deles.

A prova é um teste que executa as consultas do sistema sem recorte e falha se alguma devolver item,
previsto em testes/dominios/isolamento e ainda inexistente.

## O que não exige

Não exige segredo de cifra distinto por domínio: a decisão 0006 remete isso a decisão própria, antes
de existir o segundo domínio real. Não exige isolamento físico — se algum domínio passar a exigi-lo,
a decisão 0006 diz que ele deixa de caber na mesma instalação.
