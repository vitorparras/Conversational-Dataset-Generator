# Armazenamento

> Norma vigente. 2026-11-15.

Peça que lê e grava uma tabela. Vive em `core/src/Core.Persistence`, uma classe por agregado.

## Exemplar

`core/src/Core.Persistence/People/SignInAttempts.cs`, com o SQL em
`core/src/Core.Persistence/People/SignInStatements.cs`.

- O SQL fica todo numa classe `internal static` de constantes (`SignInStatements`); a classe de
  armazenamento só monta o comando, passa parâmetros nomeados e lê colunas por constante
  (declaradas nas linhas 18 a 21, lidas nas linhas 100 a 106).
- A escrita que precisa de registro na cadeia roda em `CustodyTransaction.AppendAfterAsync`, na
  mesma transação da linha: ou entra nas duas, ou em nenhuma (linhas 122 a 137).
- O resultado é tipo de resultado (`CustodyResult`), nunca exceção nem `null` para falha esperada.
- Leitura só lê: `SeriesAsync` não acrescenta evento e devolve lista só de leitura.

## Princípio

Responsabilidade única: a peça traduz entre linha e objeto. A regra do negócio decide antes, num
serviço de domínio ([002](002-servico-de-dominio.md)) ou num tipo pequeno que a peça devolve, como
`LabelState` em `core/src/Core.Persistence/Domains/LabelState.cs`, que se testa sem banco.

## Exceção transitória

O laço de nova tentativa do exemplar (linhas 125 a 134) é um dos cinco jeitos de hoje, o que fere
o DRY da regra 1 de [regras de código](../../regras-de-codigo.md). Até a #816 trazer o laço único,
peça nova copia o do exemplar e não cria um sexto jeito.

## Proibido

- SQL fora de uma classe de `Statements`. Hoje 42 arquivos de `core/src` ainda o têm, contra 6
  classes que seguem o exemplar; peça nova segue o exemplar, a antiga muda quando for tocada.
- Decidir domínio dentro do armazenamento: janela, limite, transição de estado ou recusa. O próprio
  exemplar tem esse resto (`Window`, `LeastRefusals` e `NetworkOf`, linhas 37 a 41 e 81 a 95), e
  `core/src/Core.Persistence/Ingestion/BatchReviews.cs` decide a revisão nas linhas 77 a 114;
  não copie nenhum dos dois trechos.
- Estado como texto livre, comparado com literal
  (`core/src/Core.Persistence/Ingestion/IngestionBatches.cs`, linha 182): estado é enumeração.
- Falha esperada por exceção: `ContentWriter` lança `BatchNotOpenException` e
  `ItemEmptiedException` para recusa do banco (linhas 100 a 110).
