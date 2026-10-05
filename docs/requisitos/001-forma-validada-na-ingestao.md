# 001 — A forma do material é validada na ingestão

> Norma vigente. 2026-10-04.

**Nada entra no acervo sem que a forma do material esteja declarada e conferida contra o conteúdo;
o que não se encaixa é recusado por inteiro, com o motivo nomeado.**

## Por que existe

A [decisão 0005](../decisoes/0005-unidades-de-analise.md) fixa quatro formas de material e manda
que cada uma tenha validação própria: conversa sem autoria por mensagem ou sem ordem temporal não é
conversa. Aceitar isso em silêncio produz acervo que parece válido e não é, e o defeito aparece só
na anotação — ou, pior, no dataset já publicado.

A recusa é do lote inteiro porque material meio ingerido é material cuja procedência ninguém sabe
descrever depois. Entre reprocessar um lote e explicar um acervo parcial, reprocessar é mais barato.

## Critérios de aceitação

- Dado um material declarado como conversa, quando alguma mensagem está sem autoria ou a ordem
  temporal não pode ser estabelecida, então a ingestão recusa o lote e o motivo identifica as
  mensagens em falta.
- Dado um lote em que parte do material é inválida, quando a ingestão termina, então nenhum item
  desse lote está no acervo.
- Dado um material que não corresponde exatamente a nenhuma das formas reconhecidas, quando ele é
  acomodado em uma delas, então o item resultante declara que forma recebeu e o que se perdeu na
  acomodação.
- Dado um item aceito, quando se pergunta qual é sua forma, então a resposta é exatamente uma das
  reconhecidas — nunca nenhuma, nunca duas.

## O que não exige

Não exige ler todo formato de arquivo existente: exige que o que foi lido chegue ao acervo numa
forma conhecida. Não exige inferir a forma automaticamente — declarar e conferir basta. Não exige
descartar o material recusado, que pode aguardar correção fora do acervo.
