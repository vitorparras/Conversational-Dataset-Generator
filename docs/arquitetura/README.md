# Arquitetura

> Descrição. O que entra nesta pasta.

Como o sistema é construído: fronteiras entre módulos, contratos, fluxo de dados, modelo de
persistência.

**Esta pasta descreve, não manda.** Se um documento aqui divergir do código, **o código ganha** — e
a divergência é um achado que merece issue, não uma correção silenciosa.

**Descreve o desenho-alvo, não só o que já existe.** Cada documento abre com uma linha "Estado no
código" que diz, citando por caminho, o que dele existe; o que ainda não existe aponta a fase do
[roadmap](../roadmap/README.md) que o instala.

O que **autoriza** uma escolha de arquitetura vive em [`decisoes/`](../decisoes/); aqui está o
resultado dela.

## Índice

Comece pela visão geral; os três seguintes dão o desenho inteiro, e os módulos detalham cada parte.

| # | Documento |
|---|---|
| [001](001-visao-geral.md) | Visão geral |
| [002](002-fronteiras.md) | Fronteiras e contratos |
| [003](003-fluxo-do-material.md) | O fluxo do material |
| [004](004-modelo-de-dados.md) | O modelo de dados |
| [005](005-modulo-ingestao.md) | Módulo de ingestão |
| [006](006-modulo-sanitizacao.md) | Módulo de sanitização |
| [007](007-modulo-pseudonimizacao.md) | Módulo de pseudonimização |
| [008](008-modulo-anotacao.md) | Módulo de anotação |
| [009](009-modulo-validacao.md) | Módulo de validação e métricas |
| [010](010-modulo-exportacao.md) | Módulo de exportação |
| [011](011-modulo-controle-de-acesso.md) | Módulo de controle de acesso |
| [012](012-modulo-custodia.md) | Módulo de custódia |

Ver [regras de escrita](../regras-de-escrita.md).
