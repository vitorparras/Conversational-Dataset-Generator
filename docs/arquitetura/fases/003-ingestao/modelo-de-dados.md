# Modelo de dados da fase 3

> Norma pendente. 2026-11-15. Os números são provisórios: o número se fixa quando o PR abre.

A última migração do main é a 0054
(`core/src/Core.Persistence/Migrations/Sql/0054-batch-review.sql`). As cinco abaixo seguem dela,
sem lacuna; se outra entrar antes, todas andam juntas. Toda coluna nova do lote entra em
refuse_batch_change por `CREATE OR REPLACE` da versão da 0054, que é a última.

## Fatia A

**`0055-batch-declared-form`.** `content.ingestion_batch.declared_form`, a forma em que o material
foi declarado. Hoje ela só vive em memória (`SubmissionStoring.Kept`, linha 150) e o item só guarda
a forma quando foi acomodado (accommodated_into, 0045, linha 7). Sem ela, a contagem por forma
de #609 CA-1 não sai do banco. Nula nos lotes que já existem, com a mesma lista de formas da 0045
(linha 10), gravada na abertura e imutável.

**`0056-item-destination-counts`.** Três inteiros em `content.item_text`: quantos trechos o item
manda remover, pseudonimizar e preservar (`OccurrenceDestination`). As ocorrências estão seladas
(0052, linha 9), e abrir cada item para contar faria da tela do lote uma leitura de conteúdo. A
contagem não é conteúdo e fica em claro. É gravada no mesmo comando que grava o item e as
ocorrências (0052, linha 1), nunca muda, e o esvaziamento a zera com o resto.

**`0057-batch-detail-counts`.** A função `batch_detail_counts(of_batch, of_domain)`, com
`SECURITY DEFINER` como batch_item_ids (0053, linha 32). Devolve só números: os itens por forma,
os trechos por destino e os itens replicados. Ela existe porque a política batch_accepted_only
(0035, linha 149; 0046, linhas 72 e 77) esconde os itens e as marcas de um lote aberto, que é
justamente o lote que a curadoria vê ao fim da submissão.

**#775, sem migração.** O motivo novo submission_cancelled cabe na regra de motivo da 0034
(linha 57).

## Fatia B

**`0058-sample-review-progress`.** Duas colunas em `content.ingestion_batch`. reviewed_through é
a posição do último item da amostra marcado como revisado, e só cresce de um em um. finding_position
é o item em que o achado foi feito, só num lote reprovado por achado. A tela já revisa na ordem
e diz em qual item o lote caiu (SampleReviewScreen.razor, linha 108), mas a 0054 só guarda quem,
quando e o achado (linhas 34 a 36). As duas entram em refuse_batch_change.

**`0059-sample-text-read`.** A função `sample_item_text(of_batch, of_item, of_domain)`, com
`SECURITY DEFINER`. Ela devolve o texto selado de um item só quando quatro coisas valem ao mesmo
tempo:

- o item está na review_sample (0053, linha 25);
- o lote está aberto e já foi sorteado;
- quem pede tem a Curadoria;
- a mesma transação já gravou o evento raw_text_read ou sanitized_text_read (0054, linha 10)
  para aquele item.

Ela devolve a versão mais tratada que existe. Sem ela, a amostra de um lote aberto não se lê, pela
mesma política da 0057.

## O que não muda

- As colunas da revisão (0054, linhas 34 a 36) e o sorteio (0053) ficam como estão; a 0058 só soma.
- A aprovação e a reprovação só mudam no domínio (ver [contratos](contratos.md)).
- Nenhuma tabela nova.

Ver [riscos](riscos.md) para o que cada migração precisa provar em teste de banco.
