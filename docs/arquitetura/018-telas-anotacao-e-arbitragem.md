# 018 — Telas de anotação e arbitragem

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Anotação e arbitragem, onde uma pessoa passa horas. A tecnologia e o atalho de teclado são da
[0010](../decisoes/0010-tecnologia-da-interface.md); os padrões comuns, da
[0051](../decisoes/0051-navegacao-gestao-e-relatorio.md).

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| N1 | Lista de trabalho | Anotação | Itens atribuídos, estado, rascunhos marcados; rótulo estável no lugar do nome | Abre o item | 7 | #619 (dado em #135) |
| N2 | Anotar | Anotação | Texto pseudonimizado, categorias, principal, dimensões, apoio, rascunho, glossário, critérios | Grava a anotação; relata dado pessoal | 7 | #126–#133 |
| B1 | Fila de arbitragem | Arbitragem | Divergências abertas do domínio | Abre | 8 | #620 (regra #149) |
| B2 | Arbitrar | Arbitragem | As duas anotações lado a lado, marcações de apoio, sem saber de quem | Resolve; os originais ficam | 8 | #150 |

## A tela de anotação, por issue

- #126 ler o item, com busca e ordenação; #127 várias categorias e a principal pelo teclado.
- #128 rascunho preservado; #129 material inteiro e escopo da leitura.
- #130 marcação de apoio; #131 relato de dado pessoal; #132 glossário; #133 decisões de critério.

## Observações

- O atalho por categoria é a razão da camada de cliente: ela captura teclado, lê a seleção e
  mostra retorno visual, sem regra de decisão. É o que o teste de navegador (#590) protege.
- A troca de região nunca recria o formulário em andamento, para consultar o manual sem perder o
  que não foi submetido (requisito 033).
- Na reanotação cega o item parece normal: nenhuma marca que o identifica chega à página.
- A B2 não mostra de quem é cada anotação, e resolver não apaga as originais (requisito 012,
  [0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md)).
- A #149 dizia que a fila tinha issue própria; a #620 a entrega.
- A acessibilidade da anotação segue o mesmo nível das outras telas (#591).
