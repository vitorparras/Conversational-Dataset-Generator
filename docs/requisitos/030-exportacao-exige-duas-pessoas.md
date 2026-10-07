# 030 — Exportar exige duas pessoas

> Norma vigente. 2026-10-07.

**A exportação de um dataset é autorizada por duas pessoas distintas: quem solicita e quem aprova.**

## Por que existe

A exportação é a operação que não tem volta. Uma vez entregue, o artefato está fora do alcance do
sistema: não se revoga, não se corrige, não se apaga do computador de quem recebeu.

Reverter um pseudônimo — operação grave, mas **reversível e contida** — já exige duas pessoas. A
exportação, que é irreversível e distribui, dependia de uma só. A assimetria não se sustenta.

Duas pessoas não impedem erro deliberado de quem tem as duas funções, e não é esse o objetivo. O que
elas impedem é o erro de uma pessoa só: recorte errado, domínio errado, limites de suficiência
ignorados por pressa.

## Critérios de aceitação

**CA-1** — *Dado* um pedido de exportação, *quando* for submetido, *então* nada é produzido até que
outra pessoa, com função que permita aprovar, o confirme.

**CA-2** — *Dado* um pedido, *quando* quem aprova for a mesma pessoa que solicitou, *então* o
sistema recusa, ainda que ela tenha as duas funções.

**CA-3** — *Dado* um pedido aprovado, *quando* o artefato for produzido, *então* a cadeia registra
as duas identidades, o momento de cada ato e o recorte exato que foi aprovado.

**CA-4** — *Dado* um pedido aprovado, *quando* o recorte mudar entre a aprovação e a produção,
*então* a aprovação perde validade e outra é exigida.

## O que não exige

Não exige que quem aprova revise o conteúdo: a aprovação é do recorte e das condições, não do
material item a item.

Não exige terceira pessoa, nem aprovação de quem forneceu o material — o contrato de acesso trata
disso, e é outro instrumento.
