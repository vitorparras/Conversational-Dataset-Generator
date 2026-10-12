# 014 — Módulos críticos e pisos de cobertura

> Operacional. 2026-10-10.

A [decisão 0007](../decisoes/0007-garantia-de-qualidade.md) manda estes números viverem aqui, e não
nela, porque mudam com o código. A verificação de código lê o piso **deste** documento: não existe
segunda cópia do número em configuração de ferramenta.

## Critério

Um módulo é crítico quando a falha dele vaza dado pessoal, expõe segredo ou corrompe a custódia: o
registro, a fronteira com o serviço de linguagem, a autenticação, a migração do banco. Falha que
produz só tela torta ou mensagem confusa não torna um módulo crítico.

## Áreas e pisos

Piso de cobertura de linha, medido por `tools/verify-code/verify.py`. Com crase, o
projeto existe hoje. Sem crase, ainda não existe: a medição é zero até a etapa que o cria, e o piso
passa a valer quando ele existir.

| Área | Projeto/pacote | Piso | Motivo |
|---|---|---|---|
| Platform.Configuration | `Core.Platform.Configuration` | 90 | Chave ignorada desliga proteção (requisito 032) |
| Platform.Logging | `Core.Platform.Logging` | 80 | O registro é por onde dado pessoal vazaria (requisito 021) |
| Platform.Startup | `Core.Platform.Startup` | 70 | Decide se o núcleo se declara apto |
| Platform.Versioning | `Core.Platform.Versioning` | 70 | Relata o que está em execução |
| Platform.Secrets | `Core.Platform.Secrets` | 0 | Só interface, sem linha executável |
| Platform.Time | `Core.Platform.Time` | 0 | Só interface, sem linha executável |
| Boundary.Text | Core.Boundary.Text | 90 | Deslocamento errado aponta para o trecho errado |
| Boundary.Http | Core.Boundary.Http | 90 | A resposta do serviço é entrada não confiável |
| Persistence.Migrations | Core.Persistence.Migrations | 90 | Esquema errado corrompe a custódia |
| Host | `Core.Host` | 50 | Cola e rotas; a falha aparece na integração |
| language_service.auth | `language_service.auth` | 85 | Sem segredo válido o serviço recusa o texto |
| language_service.logging_ | `language_service.logging_` | 90 | O registro do serviço não pode conter o texto |
| language_service.app | `language_service.app` | 80 | Monta as rotas e a ordem das recusas |
| language_service.contract | `language_service.contract` | 80 | Contrato estrito da fronteira |
| language_service.recognizers | `language_service.recognizers` | 70 | Reconhecedores atrás de um Protocol |
| tools | `tools/` | 50 | Ferramentas de verificação; falham sem vazar nada |

## Pastas críticas

Todo módulo (subpasta ou arquivo de código) de uma pasta abaixo precisa de linha na tabela de áreas.
Módulo novo sem linha reprova a verificação, que o nomeia.

| Pasta crítica | Motivo |
|---|---|
| `Core.Platform` | Registro, configuração e partida do núcleo |
| Core.Boundary | Tudo que cruza para o serviço de linguagem |
| Core.Persistence | Banco, migração e garantias do servidor |
| `language_service` | Serviço que recebe texto ainda não sanitizado |

## Teto de tempo da mutação

A mutação roda só nas áreas críticas e consome até 15 minutos na verificação de pull request. Passar
do teto reprova o passo, que não é encurtado em silêncio. Reduzir a lista é decisão escrita.

## Como baixar um piso

A descrição do pull request traz a justificativa escrita e o que mudou no código para que o piso
anterior não valha mais (um módulo dividido, um ramo removido). Sem as duas, o piso não baixa.
