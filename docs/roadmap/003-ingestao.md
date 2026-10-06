# Fase 3 — Ingestão

> Norma vigente. 2026-10-04.

O material entra. A decisão [0005](../decisoes/0005-unidades-de-analise.md) fixa quatro formas —
conversa, comentário, documento e sequência rotulada — e a ingestão é onde cada uma ganha a
validação que a distingue. Conversa sem autoria por mensagem ou sem ordem temporal não é conversa, e
aceitar isso em silêncio produz dataset que parece válido e não é.

Entra o caminho completo de entrada: leitura dos formatos de origem, validação por forma, gravação
do texto bruto já cifrado, e um elo de custódia por lote ingerido. Entra também a recusa como
resultado de primeira classe, e a perda declarada quando um material é adaptado a uma forma que não
é exatamente a dele.

Validar é o que distingue acervo de pasta de arquivos. É por isso que a ingestão é uma fase, e não
um script de carga.

## O que precisa estar pronto antes

A [fase 2](002-custodia-e-cifra.md), inteira. O texto bruto é cifrado ao gravar: antes de a cifra
existir não há lugar onde pô-lo. E todo conteúdo pertence a um domínio, cuja política de linha vem
da mesma fase.

## Critério de término

1. [001](../requisitos/001-forma-validada-na-ingestao.md) é verificado para cada uma das quatro
   formas, com ao menos um caso de recusa por forma e o motivo presente no resultado.
2. [002](../requisitos/002-origem-preservada-e-rastreavel.md) é verificado: de qualquer item se
   chega ao material como ele chegou, e ao lote em que entrou.
3. Um lote recusado não deixa material parcial no acervo.
4. Material adaptado de uma forma para outra grava a perda declarada, e a perda continua recuperável
   junto ao material depois.
5. Cada lote ingerido acrescenta elo à cadeia, e descartar o lote não apaga o elo.
6. O texto bruto gravado continua coberto por
   [003](../requisitos/003-copia-do-armazenamento-nao-expoe.md) — agora com material real, e não só
   com o caso sintético da fase anterior.

## O que destrava

A sanitização passa a ter sobre o que operar — e material cuja forma já foi validada, o que
significa que um defeito de sanitização é defeito de sanitização, não resíduo de entrada malformada
que ninguém conferiu.
