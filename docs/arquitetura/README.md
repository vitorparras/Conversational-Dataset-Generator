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
| [013](013-mapa-de-telas.md) | Mapa de telas: funções, áreas e padrões |
| [014](014-telas-entrada-sessao-e-conta.md) | Telas de entrada, sessão e conta |
| [015](015-telas-administracao-tecnica.md) | Telas da administração técnica |
| [016](016-telas-ingestao-e-acervo.md) | Telas de ingestão e do acervo |
| [017](017-telas-reversao-esvaziamento-e-relatos.md) | Telas de reversão, esvaziamento e relatos |
| [018](018-telas-anotacao-e-arbitragem.md) | Telas de anotação e arbitragem |
| [019](019-telas-campanha-avaliacao-e-parecer.md) | Telas de campanha, avaliação e parecer |
| [020](020-telas-exportacao-e-comparacao.md) | Telas de exportação e comparação |
| [021](021-telas-auditoria.md) | Telas de auditoria |
| [022](022-revisao-manual-de-acessibilidade.md) | Revisão manual de acessibilidade |

Ver [regras de escrita](../regras-de-escrita.md).
