# Modelo de dados da fase 3

> Norma pendente. 2026-11-15. Os números são provisórios: o número se fixa quando o PR abre.

A última migração do main é a 0054
(`core/src/Core.Persistence/Migrations/Sql/0054-batch-review.sql`). As cinco abaixo seguem dela,
sem lacuna; se outra entrar antes, todas andam juntas. Nomes de migração, coluna e função são
propostas e ficam em prosa até existirem. Coluna nova do lote entra em refuse_batch_change por
`CREATE OR REPLACE` da versão da 0054.

## Fatia A

**0055, a forma declarada do lote.** Uma coluna em `content.ingestion_batch` com a forma em que o
material foi declarado. Hoje ela só vive em memória (`SubmissionStoring.Kept`, linha 150), e o item
só guarda a forma quando foi acomodado (0045, linha 7). Sem ela, a contagem por forma de #609 CA-1
não sai do banco. É nula nos lotes que já existem, usa a lista de formas da 0045 (linha 10), é
gravada na abertura e não muda.

**0056, as contagens por destino do item.** Três inteiros em `content.item_text`: quantos trechos o
item manda remover, pseudonimizar e preservar (`OccurrenceDestination`). As ocorrências estão
seladas (0052, linha 9), e contar abrindo cada item faria da tela do lote uma leitura de
conteúdo. Número não é conteúdo e fica em claro, gravado no comando que grava o item. Quem guarda
`content.item_text` não é refuse_batch_change, e a 0056 estende os três guardas da 0052:

- o CHECK item_text_emptied (linhas 12 a 15) passa a exigir as contagens zeradas no esvaziado;
- o `GRANT UPDATE` (linha 18) passa a cobrir as três colunas, porque o esvaziamento as zera;
- o gatilho emptied_content_is_final (linhas 21 a 28) passa a recusar a mudança delas fora do
  esvaziamento.

**0057, as contagens do lote.** Uma função que devolve só números: os itens por forma, os trechos
por destino e os itens replicados. Ela existe porque a política batch_accepted_only (0035, linha
149; 0046, linhas 72 e 77) esconde os itens e as marcas do lote aberto, que é o lote que a
curadoria vê ao fim da submissão. É `SECURITY DEFINER`, como batch_item_ids, e fecha o acesso
como a 0053 (linhas 46 e 47): `REVOKE ALL` de PUBLIC e `GRANT EXECUTE` só para app. O banco não
sabe a função da pessoa: a Curadoria é conferida antes, na rota, por `ScreenAccess`.

**Sem migração.** O motivo submission_cancelled (#775) cabe na regra da 0034 (linha 57), e o
índice da 0051 já recusa aceitar a segunda cópia.

## Fatia B

**0058, o progresso da revisão.** Duas colunas em `content.ingestion_batch`: a última posição da
amostra marcada como revisada e a posição do item em que o achado foi feito. A 0054 só guarda quem,
quando e o achado (linhas 34 a 36). A 0058 refaz refuse_batch_change:

- a cláusula da linha 69, que recusa mudar um lote aberto fora do sorteio e da revisão, ganha o caso
  da marcação;
- a posição revisada só cresce de um, com a amostra já sorteada e sem revisão;
- a posição do achado só vale numa revisão com achado e é a posição revisada mais um.

A aprovação são dois UPDATEs na mesma transação (revisar, linha 71; aceitar, linha 77).

**0059, a leitura do texto da amostra.** Uma função `SECURITY DEFINER`, com o fechamento da 0057,
que devolve o texto de um item só se ele está na review_sample (0053, linha 25), se o lote está
aberto e sorteado e se a mesma transação já gravou o raw_text_read ou o sanitized_text_read (0054,
linha 10) daquele item. Ela devolve o sanitizado quando existe e, senão, o bruto.

Nenhuma tabela nova. O sorteio (0053) e as colunas da revisão (0054) não mudam. Ver
[riscos](riscos.md) para o que cada migração prova em teste de banco.
