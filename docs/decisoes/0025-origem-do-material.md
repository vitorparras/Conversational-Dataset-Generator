# 0025 — A origem do material: regime mais restrito, e sem divulgação

> Decisão vigente. 2026-10-07.

## Contexto

Material chega de procedências diferentes: parte com acordo que limita o uso, parte sem condição
declarada, e de boa parte não se sabe a origem nem se a fonte é confiável. Um dataset exportado
mistura esses materiais, e sem regra o pacote inteiro sai sob o regime mais permissivo — porque
ninguém marcou a diferença, e nada acusa.

Há ainda um risco distinto: artefato que declare a procedência cria, em quem recebe, um vínculo
entre conteúdo e origem — e saber de onde veio estreita quem são as pessoas, mesmo com o conteúdo
pseudonimizado.

## Decisão

**Cada material carrega, no acervo, a condição sob a qual entrou. Quando um recorte mistura
condições, prevalece sempre a mais restritiva.**

**Omissão nunca libera.** Material sem condição declarada é tratado como o mais restrito que o
sistema conhece — não como livre. Isso vale também quando a procedência é desconhecida, que é o caso
frequente.

**O artefato exportado não divulga a procedência nem a vincula a ninguém.** Ele declara o domínio,
as taxonomias, as contagens e o estado do acervo de que saiu — o que o
[requisito 014](../requisitos/014-exportacao-declara-o-recorte.md) exige, e que é informação sobre
configuração, não sobre fonte.

As duas convivem: **o sistema registra a procedência para a custódia; o artefato não a revela.**
Auditar de onde veio exige acesso ao acervo, que é contratual.

## Recusados

**Bloquear a exportação quando a origem é desconhecida.** Inviabilizaria o caso mais comum, e
trataria a ignorância como impedimento em vez de como condição a respeitar.

**Prevalecer a condição do material majoritário.** Tornaria a proteção dependente de proporção: um
único lote restrito diluído em material livre perderia a proteção por ser minoria.

**Declarar a procedência de forma agregada.** Dizer que um recorte contém material de certa origem
já estreita quem são as pessoas, e agregar não desfaz isso em conjunto pequeno.

## Consequências

O dataset não permite a quem o recebe distinguir material de origens diferentes, nem avaliar a
confiabilidade de cada fonte. É perda real de informação metodológica, aceita porque a alternativa
expõe.

Um único lote sob condição restrita torna todo recorte que o inclua igualmente restrito. Exportar
sob condição mais branda exige recorte que exclua aquele material, e essa exclusão aparece nas
contagens.

Com origem desconhecida na maioria dos casos, o regime mais restrito é o normal, não a exceção — e
o sistema precisa ser operável sob ele.

## O que reabre

Se um acervo tiver procedência inteiramente conhecida e autorizada para divulgação, a regra de não
revelar deixa de proteger alguém — e a exceção é registrada na exportação, não assumida.
