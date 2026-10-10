# 034 — Cópia vazada é atribuível

> Norma vigente. 2026-10-07.

**Cada entrega de um dataset é distinguível das demais, de modo que uma cópia encontrada fora possa
ser ligada a quem a recebeu.**

## Por que existe

O artefato exportado é idêntico para o mesmo recorte — é o que torna o resultado reproduzível, e é
garantia de outro requisito.

Essa mesma propriedade apaga a origem: duas pessoas que recebem o mesmo recorte recebem arquivos
indistinguíveis, e uma cópia encontrada em circulação não aponta para ninguém.

Sem atribuição, o contrato que restringe o uso não tem como ser verificado. Descobre-se que houve
vazamento e não se sabe de qual entrega — o que impede tanto responsabilizar quanto conter.

## Critérios de aceitação

**CA-1** — *Dado* duas entregas do mesmo recorte a pessoas diferentes, *quando* forem produzidas,
*então* cada uma carrega uma marca própria, registrada junto à entrega.

**CA-2** — *Dado* uma cópia encontrada, *quando* for examinada, *então* a marca permite identificar
a entrega de origem.

**CA-3** — *Dado* que a marca existe, *quando* o conteúdo analisável for comparado entre duas
entregas do mesmo recorte, *então* ele é idêntico — a marca não altera o material nem as contagens,
e a reprodutibilidade é preservada.

**CA-4** — *Dado* uma entrega, *quando* for registrada, *então* a cadeia guarda a quem, quando, sob
qual contrato e com qual marca.

## O que não exige

Não exige que a marca resista a quem a procure e queira removê-la: ela identifica entrega, não
impede repasse deliberado de quem conhece o mecanismo.

Não exige marca em cada item: basta que a entrega seja distinguível.
