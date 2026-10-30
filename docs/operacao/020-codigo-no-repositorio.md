# 020 — O código no repositório

> Operacional. 2026-10-29.

Onde cada parte do código vive, os comandos que a verificam e o que reprova. Os limites estão nas
[regras de código](../regras-de-codigo.md); o detalhe de cada passo, no [README](../../README.md).

## Árvore

| Pasta | O que vive lá |
|---|---|
| `core/` | O núcleo .NET: a solução `core/Core.slnx`, `core/src` e `core/tests` |
| `language-service/` | O serviço de linguagem Python: `language-service/src/language_service` |
| `tools/` | `verify-code`, `verify-docs`, `build-service`, `database` e os testes deles |

O núcleo tem cinco projetos em `core/src`: `Core.Platform` (registro, configuração, partida),
`Core.Boundary.Contracts` e `Core.Boundary` (a fronteira com o serviço), `Core.Persistence` (o
banco) e `Core.Host`, o executável que os compõe. Cada um tem o projeto de teste de mesmo nome em
`core/tests`, salvo o `Core.Boundary.Contracts`, testado em `Core.Boundary.Tests`; há ainda
`Core.Tests.Architecture`, `Core.Tests.Integration`, `Core.Tests.Properties` e `Core.Tests.Support`.
Os testes do serviço ficam em `language-service/tests`.

## Fronteiras

O desenho está em [fronteiras e contratos](../arquitetura/002-fronteiras.md). No código, o
`Core.Tests.Architecture` impõe: um módulo alcança outro só pelo `Contracts` ou pela plataforma, a
plataforma não alcança módulo, e só o `Core.Host` alcança todos. Núcleo e serviço só se falam por
HTTP em `127.0.0.1`, e o segredo da conversa entra no serviço pela entrada padrão.

## Comandos

Da raiz do repositório. Cada passo do `verify-code` é a mesma conferência da verificação contínua.

| Comando | O que faz |
|---|---|
| `python tools/verify-code/verify.py` | Todos os passos que não pedem `--base`, na ordem da CI |
| `python tools/verify-code/verify.py --list` | Lista os passos |
| `python tools/verify-code/verify.py --step <nome>` | Um passo; veja a ordem em `--list` |
| `python tools/verify-code/verify.py --step pr_size ai_attribution --base origin/main` | O tamanho do PR e a atribuição a IA |
| `python tools/verify-docs/verify.py` | A verificação de texto |
| `dotnet test core/Core.slnx` | Os testes do núcleo |
| `uv run --directory language-service pytest` | Os testes do serviço |
| `docker compose -f tools/database/compose.yml up` | O banco de desenvolvimento |

## O que reprova

- A régua de código: arquivo, método, parâmetros, aninhamento, composição e gancho de teste, pelo
  `Core.Tests.Architecture` e pelo `ruff`; formatação por `dotnet format` e `ruff`.
- A cobertura abaixo do piso da [014](014-modulos-criticos-e-pisos.md), lida pelo passo `floors`.
- A mutação de módulo crítico acima do teto de sobreviventes ou de tempo, da
  [017](017-teste-de-mutacao.md).
- O teste que sobe o serviço real sem a marca `Category=RealService`.
- O pull request acima de 30 arquivos ou 400 linhas de produção (`pr_size`).
- O commit com `Co-authored-by` de assistente de IA ou a linha "Generated with" (`ai_attribution`).
- Segredo (`secrets`), dependência vulnerável (`dependencies`) e achado do `trivy` ou do
  `static_analysis`, salvo risco aceito em `tools/verify-code/accepted-risks.toml`.
- Uma régua que aceita o defeito semeado: o passo `ruler_proof` planta o defeito e exige a recusa.
