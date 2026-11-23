# 0057 — O escopo do marcador é a conversa

> Decisão vigente. 2026-11-23.

## Contexto

O [requisito 006](../requisitos/006-pseudonimizacao-preserva-contexto.md) punha o escopo do
marcador no domínio: a mesma pessoa, em qualquer conversa do domínio, com o mesmo marcador. Ligar
conversas exige saber que dois nomes são a mesma pessoa, e o material só traz o nome salvo por quem
exportou: o mesmo contato aparece com nomes diferentes em exportações diferentes, e pessoas
diferentes aparecem com o mesmo nome. Nenhuma fonte do sistema diz quem é quem entre conversas.

A [0012](0012-pseudonimizacao-reversivel.md) pede marca estável para a mesma entidade e a justifica
pelo fio que atravessa a conversa (linhas 19 e 20), sem fixar o escopo. A correferência do serviço
de linguagem diz quais menções são a mesma entidade dentro do texto que recebe, e uma conversa
inteira cabe nesse texto.

## Decisão

**O escopo do marcador é a conversa: cada item tem os seus marcadores, e um marcador nunca se
repete em outra conversa, de nenhum domínio.** Nas formas sem fio de conversa
([0005](0005-unidades-de-analise.md)), cada item vale como uma conversa. O requisito 006 passa a
dizer isso no escopo, no primeiro critério e na propriedade; o critério dos dois domínios continua.

## Recusados

**Escopo no domínio.** Ligaria conversas pelo nome salvo, que funde pessoas distintas de mesmo
nome e separa a mesma pessoa salva com dois nomes. A fusão é a pior das duas: dá a duas pessoas o
mesmo marcador, e quem anota lê uma pessoa onde há duas.

**Escopo na mensagem.** Perde quem responde a quem dentro da conversa, que é o que torna a conversa
analisável (0005).

## Consequências

A mesma pessoa em duas conversas recebe dois marcadores: a análise entre conversas (quem intermedia,
quem aparece nas duas pontas) não se faz no acervo, e fica como melhoria futura. Piora o que o
requisito 006 defendia ao pôr o escopo no domínio.

A troca resolve a conversa nela mesma e não procura marcador em outros mapas; só a reexecução de
um item abre o mapa daquele item, com o evento de leitura, para manter os mesmos marcadores. Para
que um marcador não se repita em outra conversa, os marcadores em uso ficam numa tabela única na
instalação, sem o original.

## O que reabre

Uma fonte que identifique o participante entre conversas sem depender do nome salvo, como um
identificador estável que o material passe a trazer, devolve a ligação entre conversas à mesa.
