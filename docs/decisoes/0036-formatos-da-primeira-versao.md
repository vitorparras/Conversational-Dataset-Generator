# 0036 — Os formatos de origem da primeira versão

> Decisão vigente. 2026-10-07.

## Contexto

A ingestão lê material de fora, e cada formato de origem exige um leitor próprio. Nenhum documento
dizia quais formatos a primeira versão precisa ler, e a ausência deixava o escopo da fase de
ingestão indefinido.

## Decisão

**A primeira versão lê conversa exportada, e só.** Formato em que o material já chega com remetente,
ordem e momento separados, sem necessidade de inferência.

Cada formato entra atrás de um leitor substituível
([decisão 0005](0005-unidades-de-analise.md) define a unidade que todos produzem). Acrescentar um
segundo formato depois não mexe no primeiro.

## Recusados

**Conversa exportada, PDF e HTML.** Cobriria os três formatos em que material conversacional chega
na prática, e triplicaria a superfície que recebe conteúdo de fora na fase em que a validação de
forma ainda está sendo construída. PDF e HTML exigem inferir o que a conversa exportada já declara —
quem falou, em que ordem, quando — e inferência errada produz dataset com autoria trocada, que é o
modo de falha mais grave da ingestão.

**Texto puro.** Serviria para teste e para material já transcrito, e não traz remetente nem momento:
a ordem tem de ser inferida da posição no arquivo, que é precisamente o risco que a validação de
forma existe para pegar. Material sintético de teste não precisa de formato próprio — pode ser
gerado na forma da conversa exportada.

## Consequências

Material que só existe em PDF ou HTML fica fora até alguém escrever o leitor. Isso é limitação
declarada, não defeito: a arquitetura do leitor substituível é o que torna o acréscimo barato, e a
decisão de quando fazê-lo passa a ser do operador, com o custo à vista.

A validação de forma fica mais sólida, porque há um formato só a validar — e ela é a única barreira
entre um arquivo mal lido e um dataset com autoria trocada.

## O que reabre

Quando houver material relevante que só exista em outro formato. O leitor novo entra pela mesma
abstração, e a validação de forma dele é escrita antes dele.
