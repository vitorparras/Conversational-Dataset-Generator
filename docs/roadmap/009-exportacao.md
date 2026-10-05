# Fase 9 — Exportação

> Norma vigente. 2026-10-04.

O dataset sai, e sai dizendo de onde veio. A decisão
[0006](../decisoes/0006-dominios-coexistem.md) é direta: a exportação declara o recorte no próprio
artefato. Sem isso, um dataset de conjunto misto é indistinguível de um de origem única, e deixa de
ser citável.

Entram o recorte por domínio ou por conjunto declarado deles, a validação dos rótulos contra a
taxonomia na saída, a auditoria de resíduo que bloqueia a exportação quando encontra algo, e o
conteúdo que faz do artefato uma peça autossuficiente: definições de categoria, anotações
individuais por anotador pseudonimizado, contagens e medidas. O requisito
[016](../requisitos/016-metricas-recalculaveis.md) é o mais difícil da fase, porque ele muda o que
sai e não apenas como sai — métrica que o artefato não permita recalcular não é publicada.

Exportar é evento de custódia: o que saiu, quando, por quem e sob qual recorte fica na cadeia.

## O que precisa estar pronto antes

A [fase 7](007-anotacao.md), pela taxonomia; a [fase 8](008-concordancia.md), pelas medidas; a
[fase 2](002-custodia-e-cifra.md), pelo domínio e pela cadeia. É a fase com mais pré-requisitos, e é
por isso que vem tarde: ela publica, e o que foi publicado não se despublica.

## Critério de término

1. [014](../requisitos/014-exportacao-declara-o-recorte.md) é verificado lendo apenas o artefato,
   sem acesso ao sistema que o produziu.
2. [015](../requisitos/015-exportacao-sem-identidade.md) é verificado, inclusive o bloqueio: um
   artefato com resíduo plantado de propósito não é exportado.
3. [016](../requisitos/016-metricas-recalculaveis.md) é verificado por recálculo independente, a
   partir do artefato, chegando aos mesmos valores.
4. Rótulo fora da taxonomia do domínio bloqueia a exportação, fechando a ponta de saída de
   [008](../requisitos/008-rotulo-dentro-da-taxonomia.md).
5. Exportar duas vezes a partir do mesmo estado do acervo produz artefatos idênticos.
6. [022](../requisitos/022-operacao-sem-servico-externo.md) é verificado de ponta a ponta: o fluxo
   completo, do material à exportação, conclui com a instalação isolada da rede.
7. Cada exportação acrescenta elo à cadeia de custódia.

## O que destrava

A comparação entre domínios, que é a forma final de validação do método — e que depende de os dois
recortes serem declarados exatamente do mesmo modo.
