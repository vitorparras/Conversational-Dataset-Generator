# 0055 — Recusas da revisão por amostra em `Core.Ingestion.Contracts`

> Decisão vigente. 2026-11-17.

## Contexto

A revisão por amostra recusa um passo pelo estado do lote (não existe, não está aberto, já foi
sorteado, não foi sorteado, já foi revisado). Essa decisão vivia em `BatchReviews`, no
`Core.Persistence`, e o domínio da revisão, em `Core.Ingestion`, não podia usá-la: a regra de
fronteira recusa `Core.Persistence → Core.Ingestion`, e um módulo só alcança outro pelo `Contracts`
ou pela plataforma ([002](../arquitetura/002-fronteiras.md)). Duplicar a regra nos dois lados faria
o banco e o domínio divergirem em silêncio.

## Decisão

**A decisão das recusas é uma função pura em `Core.Ingestion.Contracts`, e os dois lados a chamam.**

1. **Onde:** o projeto novo `Core.Ingestion.Contracts` leva `SampleStateRules.Refusal`, que recebe
   o estado lido (`SampledBatch`: aberto, sorteado, revisado) e devolve o motivo (`SampleRefusal`)
   ou nada. Não lê nem escreve.
2. **Quem chama:** `BatchReviews` a chama dentro da mesma transação, sobre o lote que leu ali, e
   traduz o motivo em `BatchReviewOutcome`; o `UPDATE` condicional continua fechando a corrida.
   A mesa da revisão (`SampleReviewDesk`) chama a mesma tradução, `BatchReviews.Refusal`, antes de
   gravar; só a ordem dos lugares da amostra fica no `SampleProgress`.
3. **O que o módulo de contratos leva:** tipos e funções puras, sem E/S. A regra de fronteira e o
   `ModuleBoundaryTests` não mudam, e não há exceção.

## Alternativas recusadas

- **`BatchReviews` delegar a `SampleProgress`:** exigiria `Core.Persistence → Core.Ingestion`, que a
  regra recusa.
- **Duplicar a regra no domínio:** duas cópias que divergem sem que nenhum teste note.
- **Mover o tipo de estado do banco para o contrato:** arrasta `BatchReview` e a leitura da linha
  para um módulo que não deve conhecer o banco.

## Consequências

Uma regra, um lugar, testada sem banco. **Muda:** um lote que não existe passa a responder
`NotFound` na mesa, e não `NotSampled`, e um passo sobre lote fechado ou já revisado é recusado pela
mesma regra antes de chegar ao banco. **Piora:** mais um projeto, com área de cobertura e de
mutação próprias, e a tradução fina de `SampleRefusal` para `BatchReviewOutcome` no banco.

## O que reabre esta decisão

Um contrato que passe a precisar de E/S ou de um tipo do banco; uma regra de estado que o banco
tome e o domínio não, ou o contrário.
