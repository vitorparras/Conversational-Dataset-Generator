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

O piso é a porcentagem de linhas cobertas pela suíte da linguagem da área. **Crítica** diz `sim` ou
`não` em toda linha e é a única marca de módulo crítico: a mutação lê a coluna, não o piso. O
cabeçalho é fixo e lido pela verificação; linha nova segue o mesmo formato. Projeto/pacote vale
como **namespace e pasta**: o passo `floors` reprova o arquivo C# de `core/src` que cai numa área
pelo namespace e noutra pela pasta, nomeando as duas. No serviço, módulo e arquivo coincidem por
construção. Código fora de toda área listada conta na do projeto ou pacote que o contém, pelo
prefixo mais longo que casar.

| Área | Projeto/pacote | Piso | Crítica | Motivo |
|---|---|---|---|---|
| Registro do núcleo | `Core.Platform.Logging` | 90 % | sim | Classifica e redige o que vai ao registro (requisito 021) |
| Deslocamento de texto | `Core.Boundary.Text` | 90 % | sim | Única aritmética de índice sobre texto do serviço (requisito 009) |
| Reconhecedor HTTP | `Core.Boundary.Http` | 90 % | sim | Texto não sanitizado cruza o processo e a resposta é validada aqui (decisão 0018) |
| Análise do material | `Core.Host.Analysis` | 90 % | sim | Leva o texto ao reconhecedor e decide o que a recusa registra e devolve (requisito 021) |
| Contrato da fronteira | `Core.Boundary.Contracts` | 90 % | sim | A forma JSON recusa campo desconhecido ou nulo no texto que cruza o processo (decisão 0018) |
| Migração do esquema | `Core.Persistence.Migrations` | 90 % | sim | Altera o esquema do banco; erro corrompe custódia (requisito 019) |
| Autenticação do serviço | `language_service.auth` | 90 % | sim | Guarda o segredo que protege texto não sanitizado (decisão 0011) |
| Registro do serviço | `language_service.logging_` | 90 % | sim | Decide o que o serviço escreve em registro (requisito 021) |
| Configuração | `Core.Platform.Configuration` | 80 % | não | Falha fechada na partida (requisito 032); não toca material |
| Partida | `Core.Platform.Startup` | 80 % | não | Conferências que impedem subir sem garantia; sem material |
| Plataforma, demais pastas | `Core.Platform` | 80 % | não | Versões, relógio e fonte da chave: portas e metadado, sem material |
| Garantias do servidor | `Core.Persistence.ServerGuarantees` | 80 % | não | Confere o banco sem alterá-lo; falha é recusa de subir |
| Persistência, demais pastas | `Core.Persistence` | 80 % | não | Conexão com credencial da aplicação; sem consulta de domínio |
| Fronteira, demais pastas | `Core.Boundary` | 80 % | não | Proposta de piso, sujeita à aprovação do dono: o código está nas subáreas acima, esta linha cobre o resto |
| Serviço, demais módulos | `language_service` | 80 % | não | Rotas, contrato e reconhecedor vazio; sem segredo nem registro |
| Reconhecedor falso | `Core.Boundary.Fake` | 60 % | não | Dublê determinístico de teste; nunca vê material real |
| Executável | `Core.Host` | 60 % | não | Só composição, sem regra; coberto por teste de integração |
| Ferramentas | `tools` | 60 % | não | Scripts da verificação; falha reprova o build, não vaza dado |

`isento` é o único valor não numérico aceito: a área está declarada e nada se compara. Área medida
fora da tabela reprova; piso ausente não é piso zero. Área com piso e sem linha a cobrir (`0 de 0`)
aparece sem código e não reprova, salvo se o projeto ou pacote existe e nenhum relatório o leu.

O teste de mutação percorre as áreas com `sim` em Crítica; as regras dele estão na
[017](017-teste-de-mutacao.md).

## Como um piso muda

Subir um piso ou acrescentar área é mudança deste documento, revisada como qualquer outra. Módulo
novo que atende ao critério entra na tabela no mesmo pull request que o cria. Baixar um piso exige
decisão escrita em `docs/decisoes/`, com a justificativa e o que mudou para que o valor anterior
não valha mais. A decisão entra antes, em pull request próprio, nunca no mesmo que faz a cobertura
cruzar o piso para baixo.
