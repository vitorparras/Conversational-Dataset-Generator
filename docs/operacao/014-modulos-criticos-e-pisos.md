# 014 — Módulos críticos e pisos de cobertura

> Operacional. 2026-10-12.

A [decisão 0007](../decisoes/0007-garantia-de-qualidade.md) manda a lista de módulos críticos e os
pisos viverem aqui. A verificação lê os números **desta tabela**: não há segunda cópia em arquivo.

**Os números abaixo são proposta e aguardam aprovação do operador no pull request que os introduz.**

## Critério de criticidade

Um módulo é crítico quando toca material, segredo, registro de operação, deslocamento de texto ou
esquema do banco: a falha nele vaza dado ou corrompe custódia, em vez de produzir tela torta.

## Áreas, pisos e motivos

O piso é a porcentagem de linhas cobertas pela suíte da linguagem da área. **Crítica** diz `sim` ou
`não` em toda linha e é a única marca de módulo crítico: a mutação lê a coluna, não o piso. O
cabeçalho é fixo e lido pela verificação. Projeto/pacote vale como **namespace e pasta**: o passo
`floors` reprova o arquivo C# de `core/src` que cai numa área pelo namespace e noutra pela pasta,
nomeando as duas. No serviço, módulo e arquivo coincidem por construção. Código fora de toda área
listada conta na do projeto ou pacote que o contém, pelo prefixo mais longo que casar. O Motivo da
linha "demais" lista após `Absorve:`, até o `;`, as pastas de primeiro nível (módulos, no serviço)
que ela cobre. O julgamento do `floors` está na [019](019-leitura-da-tabela-pelo-floors.md). Área
ainda sem projeto entra na tabela pelo nome, por exceção declarada à [regra 5](../regras-de-escrita.md).

| Área | Projeto/pacote | Piso | Crítica | Motivo |
|---|---|---|---|---|
| Registro do núcleo | `Core.Platform.Logging` | 90 % | sim | Classifica e redige o que vai ao registro (requisito 021) |
| Chave-mestra | `Core.Platform.Keys` | 90 % | sim | Guarda a chave em memória, recusa subir sem ela e carrega os rótulos de derivação (decisões 0003, 0013 e 0014) |
| Material do canal | `Core.Platform.Tls` | 90 % | sim | Lê a chave privada do canal cifrado e recusa permissão aberta, chave fora do par e certificado fora da validade (decisões 0029 e 0046) |
| Deslocamento de texto | `Core.Boundary.Text` | 90 % | sim | Única aritmética de índice sobre texto do serviço (requisito 009) |
| Reconhecedor HTTP | `Core.Boundary.Http` | 90 % | sim | Texto não sanitizado cruza o processo e a resposta é validada aqui (decisão 0018) |
| Reconhecedor falso | `Core.Boundary.Fake` | 90 % | sim | Recebe o texto do material e devolve os deslocamentos; determinístico, selecionável por `Recognizer:Kind`; recusa texto sem marcador sintético (decisão 0011) |
| Análise do material | `Core.Host.Analysis` | 90 % | sim | Leva o texto ao reconhecedor e decide o que a recusa registra e devolve (requisito 021) |
| Contrato da fronteira | `Core.Boundary.Contracts` | 90 % | sim | A forma JSON recusa campo desconhecido ou nulo no texto que cruza o processo (decisão 0018) |
| Migração do esquema | `Core.Persistence.Migrations` | 90 % | sim | Altera o esquema do banco; erro corrompe custódia (requisito 019) |
| Transação de conteúdo | `Core.Persistence.Content` | 90 % | sim | Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão 0006) |
| Registro de pessoas | `Core.Persistence.People` | 90 % | sim | Grava o histórico de quem fez o quê com cada pessoa e decide a entrada de quem foi desativada (política 006) |
| Autenticação do serviço | `language_service.auth` | 90 % | sim | Guarda o segredo que protege texto não sanitizado (decisão 0011) |
| Prova do segredo | `language_service.proof` | 90 % | sim | Prova que conhece o segredo pelo HMAC do nonce, sem revelá-lo (decisão 0043) |
| Registro do serviço | `language_service.logging_` | 90 % | sim | Decide o que o serviço escreve em registro (requisito 021) |
| Configuração | `Core.Platform.Configuration` | 80 % | não | Falha fechada na partida (requisito 032); não toca material |
| Partida | `Core.Platform.Startup` | 80 % | não | Conferências que impedem subir sem garantia; sem material |
| Plataforma, demais pastas | `Core.Platform` | 80 % | não | Absorve: `Versions`; versões e relógio: portas e metadado, sem material |
| Garantias do servidor | `Core.Persistence.ServerGuarantees` | 80 % | não | Confere o banco sem alterá-lo; falha é recusa de subir |
| Persistência, demais pastas | `Core.Persistence` | 80 % | não | Absorve: `Connection`; conexão com credencial da aplicação, sem consulta de domínio |
| Fronteira, demais pastas | `Core.Boundary` | 80 % | não | O que fica fora de Text, Http, Fake e Contracts; sem material próprio |
| Serviço, demais módulos | `language_service` | 80 % | não | Absorve: `app`, `contract`, `gate`, `recognizers`, `routes`, `server`, `startup`; rotas, contrato e reconhecedor vazio, sem segredo nem registro |
| Executável | `Core.Host` | 60 % | não | Absorve: `Endpoints`, `Migration`, `Supervision`; só composição, sem regra, coberto por teste de integração |
| Ferramentas | `tools` | 60 % | não | Scripts da verificação; falha reprova o build, não vaza dado |

O teste de mutação percorre as áreas com `sim` em Crítica, pelas regras da [017](017-teste-de-mutacao.md).

## Como um piso muda

Subir piso ou acrescentar área é mudança deste documento, revisada como qualquer outra. Módulo
novo que atende ao critério entra na tabela no pull request que o cria. Baixar um piso exige
decisão em `docs/decisoes/`, com a justificativa e o que mudou para o valor anterior não valer
mais. A decisão entra antes, em pull request próprio, nunca no que faz a cobertura cruzar o piso.
