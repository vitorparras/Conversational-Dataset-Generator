# 052 — A imagem do banco de teste na verificação

> Operacional. 2026-11-16.

Os testes de banco e a [mutação](017-teste-de-mutacao.md) sobem um Postgres de teste a partir de
`tools/database`. A verificação não o constrói a cada job: usa uma imagem do GHCR, com a etiqueta
igual a um hash de 16 caracteres dos arquivos dessa pasta (caminho e conteúdo). Arquivo mudou, a
etiqueta muda; nada mudou, a imagem é a mesma.

## Quem publica e quem só lê

- **Publica:** só o job "Imagem do banco de teste", e só num run do `main`. É o único com
  `packages: write`. Constrói se a etiqueta não existe, empurra e termina.
- **Só lê:** todos os outros jobs que usam banco, a matriz de mutação e o build remoto. Têm
  `packages: read`: baixam a imagem se existe; se não, constroem local e não empurram. Assim, um
  pull request nunca grava no pacote, e o que ele constrói só vale para o job dele.
- O login no GHCR é desfeito (`docker logout`) antes de o job rodar o código do pull request.
- O job que publica faz o checkout com `persist-credentials: false`.

## Se a imagem não vem

O pacote é privado por padrão. Fork e dependabot não leem: constroem local, sem aviso de erro. Um
pull falho por outro motivo que não "manifest unknown" vira `::notice::` no log, e a construção
local segue. Se a construção falha, o resumo do job traz as linhas `E:` do `apt`.

## Limpar etiquetas antigas

Cada mudança de `tools/database` deixa uma etiqueta. Apague as velhas com
`gh api -X DELETE /user/packages/container/<nome>/versions/<id>`.
