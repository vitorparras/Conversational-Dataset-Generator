# Modelo de dados da fase 5

> Norma pendente. 2026-11-22. Os nomes são provisórios: o instante do nome se fixa quando o PR abre.

As migrações novas são datadas ([0054](../../../decisoes/0054-teto-por-fatia-e-migracao-por-data.md),
item 2) e vêm depois da última do main, a sanitization-rerun da fase 4. Cada passo novo da
cadeia refaz custody_event_step_listed, o motivo e a forma da referência com a lista inteira, como
aquela fez (`core/src/Core.Persistence/Migrations/Sql/202611201200-sanitization-rerun.sql`,
linhas 103 a 136). A fatia E não tem migração.

## Fatia A

**`date-shift`.** content.date_shift, uma linha por domínio, criada como as outras tabelas de
conteúdo: o deslocamento selado com a finalidade nova deslocamento-datas e a chave da linha (0049),
e quando foi sorteado. A linha nunca muda: GRANT só de INSERT, e um índice único por domínio faz o
segundo sorteio perder para o primeiro. A finalidade nova entra em `LabelPurpose` e nos rótulos
de cada domínio, pela custódia da [0013](../../../decisoes/0013-chave-de-cifra-por-dominio.md).

## Fatia B

**`pseudonymization`.** Quatro mudanças:

- content.domain_marker: domínio, marcador e tipo, em claro, único por domínio. É o que o sorteio
  consulta para não repetir, e não guarda o original.
- content.item_marker: item e marcador, em claro, e se é o autor; é o escopo que o pedido confere
  sem abrir o mapa.
- A coluna pseudonymized_text passa de text a bytea, selada com a chave do item e marcada
  `protected:texto-bruto` (hoje é nula em todo item, então nada se converte). Sai de `Clear` e entra
  em `Protected` em `ContentColumns`. É a opção da 0030, pendente de decisão do dono.
- O gatilho de item_resanitized da fase 4 passa a exigir o pseudonimizado e o mapa novos na mesma
  transação.

Os passos novos: batch_pseudonymized (um por lote, com as contagens por tipo) e reversal_map_lookup
(um por lote, com quantos mapas o sorteio abriu); o motivo pseudonym_of_removed, para a entrada de
destino remover recusada. O esvaziamento já zera o mapa (0010, linha 21) e passa a apagar
os marcadores do item.

## Fatia C

**`reversal-request`.** content.reversal_request (item, marcador, quem pediu e quando, por que, o
que fará, por quanto tempo) e content.reversal_authorization (pedido, quem autorizou, quando e até
quando), as duas só de INSERT. Um gatilho recusa quem autoriza igual a quem pediu e prazo
no passado. Os passos reversal_requested, reversal_authorized e reversal_declined, e os motivos
reversal_scope, reversal_function, reversal_self e reversal_chain_broken.

## Fatia D

**`reversal-revelation`.** content.reversal_revelation: a autorização, única, e quando. Uma linha
por autorização é o uso único; nada além disso. O passo reversal_revealed, com quem pediu, quem
autorizou, a justificativa, o marcador, o item, a finalidade e o instante; os motivos
reversal_expired e reversal_used. Nenhuma coluna guarda o valor revelado.

## O que não muda

O bruto, o sanitizado, o rastro e as ocorrências não mudam. As datas do acervo ficam como vieram:
o deslocamento só se aplica na exportação, da fase 9.

Ver [riscos](riscos.md) para o que cada migração precisa provar em teste de banco.
