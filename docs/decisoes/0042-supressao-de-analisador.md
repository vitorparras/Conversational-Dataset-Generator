# 0042 — Nenhum analisador se suprime em `core/src`

> Decisão vigente. 2026-10-16, decisão do operador; emendada em 2026-10-18 e 2026-10-25.

## Contexto

O build roda os analisadores da plataforma e o SonarAnalyzer como erro. A revisão do pull request
que ligou o Sonar achou rotas, todas compiladas sem aviso, que calam um analisador sem tocar em
regra alguma: `#pragma`, `[SuppressMessage]`, `NoWarn`, ruleset, `DiagnosticSuppressor`, gerador
que emite supressão, arquivo tomado por código gerado e config que baixa a severidade em massa.
Sem política escrita, cada guarda cobria um analisador e deixava as outras rotas abertas.

## Decisão

Em `core/src` nenhum analisador se suprime no código: nem `#pragma warning disable`, nem
`[SuppressMessage]` ou `[UnconditionalSuppressMessage]`, nem `NoWarn`, nem ruleset, nem marca de
código gerado. A única exceção é RS0030 nas classes `*LogMessages`, com `Justification`.

Uma regra de analisador só se desliga no `.editorconfig` da raiz, com o motivo escrito numa linha
de comentário logo acima: S1309, S1451 e S4055 em todo `*.cs` (S4055 pede `ResourceManager`, mas a
decisão 0039 fixa um idioma só); qualquer outra só numa seção que nomeia o arquivo. Hoje são quatro:

- S6585 em `core/src/Core.Platform/Logging/LineConsoleFormatter.cs`: o registro é lido por máquina,
  em formato fixo;
- S1200 em `core/src/Core.Host/CompositionRoot.cs`: a raiz depende de todos os serviços por
  definição, e a regra 3 a faz a única a construí-los;
- S1312 em `core/src/Core.Host/Supervision/LanguageServiceSupervisor.cs`: a DI injeta o logger, que
  não pode ser estático;
- CA2100 em `core/src/Core.Persistence/Migrations/StepCommand.cs`: o texto é recurso embutido.

O compilador recebe só o `.editorconfig` da raiz e os configs do SDK. Os arquivos que o SDK gera
e os supressores que o SDK traz são exceções nomeadas, cada uma com o motivo, no teste.

## Recusados

- **Restringir a política ao Sonar.** Pragma, ruleset e config calam qualquer analisador.
- **Supressão local com `Justification`.** Texto livre que nada confere; cada uso abre o seguinte.
- **Exclusão por categoria ou em massa.** Desliga o que ninguém nomeou; a categoria do Sonar tem
  espaço, que o compilador descarta.

## Como se impõe

O canário do log e o do Sonar plantam achados em cada tipo de cada fonte, de uma regra ligada no
`.editorconfig` e de uma ligada por padrão, e exigem o erro no build real. As regras da invocação
do compilador julgam os configs recebidos, a severidade efetiva por id e em massa, `generated_code`,
o ruleset, o analisador carregado e as opções de aviso. As da saída da build julgam os arquivos
gerados (só na raiz da pasta intermediária, com nome e forma esperados, ou de gerador nomeado) e os
supressores. Cada rota tem uma fixture que a guarda recusa.

## Consequências

A causa se corrige no código, nunca se cala. O custo é o tempo da suíte de arquitetura, que passa a
compilar projetos de verdade; uma regra que não convém precisa de motivo escrito no `.editorconfig`.

## O que reabre

Se uma regra de analisador sem alternativa de correção no código exigir supressão em três casos
seguidos, a política é revista por decisão escrita, nunca por exceção local.
