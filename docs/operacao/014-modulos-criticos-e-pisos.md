# 014 — Módulos críticos e pisos de cobertura

> Operacional. 2026-10-12.

A [decisão 0007](../decisoes/0007-garantia-de-qualidade.md) manda a lista de módulos críticos e os
pisos de cobertura viverem aqui, porque mudam com o código. A verificação automática lê os números
**desta tabela**: não existe segunda cópia em arquivo de configuração de ferramenta.

**Os números abaixo são proposta e aguardam aprovação do operador no pull request que os introduz.**

## Critério de criticidade

Um módulo é crítico quando toca material, segredo, registro de operação, deslocamento de texto ou
esquema do banco: a falha nele vaza dado ou corrompe custódia, em vez de produzir tela torta.

## Áreas, pisos e motivos

O piso é a porcentagem de linhas cobertas pela suíte da linguagem da área. O cabeçalho da tabela é
fixo e lido pela verificação; linha nova segue o mesmo formato. Código fora de toda área listada
conta na área do projeto ou pacote que o contém, pelo prefixo mais longo que casar.

| Área | Projeto/pacote | Piso | Motivo |
|---|---|---|---|
| Registro do núcleo | `Core.Platform.Logging` | 90 % | Crítica: classifica e redige o que vai ao registro (requisito 021) |
| Deslocamento de texto | `Core.Boundary.Text` | 90 % | Crítica: única aritmética de índice sobre texto do serviço (requisito 009) |
| Reconhecedor HTTP | `Core.Boundary.Http` | 90 % | Crítica: texto não sanitizado cruza o processo e a resposta é validada aqui (decisão 0018) |
| Análise do material | `Core.Host.Analysis` | 90 % | Crítica: leva o texto ao reconhecedor e decide o que a recusa registra e devolve (requisito 021) |
| Contrato da fronteira | `Core.Boundary.Contracts` | 90 % | Crítica: a forma JSON recusa campo desconhecido ou nulo no texto que cruza o processo (decisão 0018) |
| Migração do esquema | `Core.Persistence.Migrations` | 90 % | Crítica: altera o esquema do banco; erro corrompe custódia (requisito 019) |
| Autenticação do serviço | `language_service.auth` | 90 % | Crítica: guarda o segredo que protege texto não sanitizado (decisão 0011) |
| Registro do serviço | `language_service.logging_` | 90 % | Crítica: decide o que o serviço escreve em registro (requisito 021) |
| Configuração | `Core.Platform.Configuration` | 80 % | Falha fechada na partida (requisito 032); não toca material |
| Partida | `Core.Platform.Startup` | 80 % | Conferências que impedem subir sem garantia; sem material |
| Plataforma, demais pastas | `Core.Platform` | 80 % | Versões, relógio e fonte da chave: portas e metadado, sem material |
| Garantias do servidor | `Core.Persistence.ServerGuarantees` | 80 % | Confere o banco sem alterá-lo; falha é recusa de subir |
| Persistência, demais pastas | `Core.Persistence` | 80 % | Conexão com credencial da aplicação; sem consulta de domínio |
| Fronteira, demais pastas | `Core.Boundary` | 80 % | Proposta de piso, sujeita à aprovação do dono: o código está nas subáreas acima, esta linha cobre o resto |
| Serviço, demais módulos | `language_service` | 80 % | Rotas, contrato e reconhecedor vazio; sem segredo nem registro |
| Reconhecedor falso | `Core.Boundary.Fake` | 60 % | Dublê determinístico de teste; nunca vê material real |
| Executável | `Core.Host` | 60 % | Só composição, sem regra; coberto por teste de integração |
| Ferramentas | `tools` | 60 % | Scripts da verificação; falha reprova o build, não vaza dado |

`isento` é o único valor não numérico aceito: a área está declarada e nada se compara. Área medida
fora da tabela reprova; piso ausente não é piso zero. Área com piso e sem linha a cobrir (`0 de 0`)
aparece sem código e não reprova, salvo se o projeto ou pacote existe e nenhum relatório o leu.

## Mutação

O teste de mutação roda sobre as áreas de piso 90 %, que são a lista de módulos críticos. Por
módulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: **20 minutos por
linguagem** dentro da verificação de pull request. Exceder o teto é achado que reprova, nomeando o
módulo e o tempo gasto, nunca lentidão tolerada. Mutação fora do bloqueio só por decisão escrita.

## Como um piso muda

Subir um piso ou acrescentar área é mudança deste documento, revisada como qualquer outra. Módulo
novo que atende ao critério entra na tabela no mesmo pull request que o cria. Baixar um piso exige
decisão escrita em `docs/decisoes/`, com a justificativa e o que mudou para que o valor anterior
não valha mais. A decisão entra antes, em pull request próprio, nunca no mesmo que faz a cobertura
cruzar o piso para baixo.
