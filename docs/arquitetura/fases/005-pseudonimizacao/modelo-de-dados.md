# Modelo de dados da fase 5

> Norma pendente. 2026-11-22. Os nomes são provisórios: o instante do nome se fixa quando o PR abre.

As migrações novas são datadas ([0054](../../../decisoes/0054-teto-por-fatia-e-migracao-por-data.md),
item 2) e vêm depois da última do main, a sanitization-rerun da fase 4. Cada passo novo da
cadeia refaz a lista de passos, de motivos, de tipos de item e as formas, como aquela fez
(`core/src/Core.Persistence/Migrations/Sql/202611201200-sanitization-rerun.sql`, linhas 99 a 140).
O evento só aponta a linha (tipo e id do item); o que o ato diz fica na linha. As fatias E, F1
e F2 não têm migração.

## Fatia A

**`date-shift`.** content.date_shift, uma linha por domínio, como as outras tabelas de conteúdo: o
deslocamento selado com a finalidade nova deslocamento-datas e a chave da linha (0049), e quando foi
sorteado. A linha nunca muda (SELECT e INSERT, sem UPDATE nem DELETE), e um índice único por domínio
faz o segundo sorteio perder para o primeiro. A finalidade nova entra em `LabelPurpose` e nos
rótulos de cada domínio (0013). Não é única entre domínios (fatia A, #108 CA-3).

## Fatia B

**`pseudonymization`.** content.marker_in_use (marcador e tipo, único na instalação) e
content.item_marker (item, marcador único e se é o autor), sob a política da 0006: o índice único
recusa o número usado, sem ler outros domínios. Em claro (0003, linha 21). O marcador nunca sai de
marker_in_use (CE006); em item_marker, um marcador novo ao item entra só se ganho na mesma
transação (0057), e a linha sai só com item_emptied, item_resanitized ou o lote rejeitado (CE005).
pseudonymized_text passa a bytea selada com a chave do item, `protected:texto-bruto` (0030, l. 24).
O banco não lê o selado: a restrição adiada pseudonymized_needs_map exige no commit o mapa do item
na mesma transação, e o gatilho do lote exige batch_pseudonymized no aceite. No lote aberto, gatilho
e tela leem pelo escopo do sistema (batch_holds_pseudonymized, batch_marker_counts).

Os passos novos: batch_pseudonymized, um por lote aceito com item pseudonimizado, gravado no aceite
e na mesma transação, nunca antes do primeiro item; e o motivo pseudonym_of_removed, que rejeita o
lote ao chegar entrada de outro destino. Ler um mapa usa reversal_entries_read, um por mapa. O
esvaziamento zera o mapa (0010, linha 21) e apaga os marcadores; a reexecução da B só os guarda.

**F3, `pseudonymization-wiring`.** marker_in_use ganha o lote, e item_marker perde o único por
marcador: o marcador novo ao item vem do sorteio da transação ou do lote do item, nunca de outro
(0057). O item_resanitized passa a exigir o pseudonimizado e o mapa novos, e o anterior é esvaziado
(UPDATE só nas colunas do esvaziamento). O motivo pseudonym_without_pair recusa o item sem par.

## Fatia C

**`reversal-request`.** content.reversal_request (item, marcador, quem pediu e quando, prazo pedido
em dias; o porquê e o uso selados com a finalidade do mapa e a chave da linha) e
content.reversal_authorization (pedido, quem autorizou, quando e até quando), sem UPDATE nem DELETE.
Um gatilho recusa quem autoriza igual a quem pediu, prazo no passado e segunda autorização em vigor.
Os passos, os tipos de item novos e o que cada evento aponta estão em
[eventos da reversão](eventos-da-reversao.md); a R2 lê o texto do pedido por `ProtectedContentReader`.

## Fatia D

**`reversal-revelation`.** content.reversal_revelation: a autorização, única, e quando. Uma linha
por autorização é o uso único, sem UPDATE nem DELETE. Os passos reversal_revealed e reversal_denied,
em [eventos da reversão](eventos-da-reversao.md). Nenhuma coluna guarda o valor revelado.

## O que não muda

O bruto, o sanitizado, o rastro e as ocorrências não mudam. As datas do acervo ficam como vieram:
o deslocamento só se aplica na exportação, da fase 9.
