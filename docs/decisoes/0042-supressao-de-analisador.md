# 0042 — Nenhum analisador se suprime em `core/src`

> Decisão vigente. 2026-10-16, decisão do operador; emendada em 2026-10-18.

## Contexto

O build roda os analisadores da plataforma e o SonarAnalyzer como erro. A revisão do pull request
que ligou o Sonar achou rotas, todas compiladas sem aviso, que calam um analisador sem tocar em
regra alguma: `#pragma`, `[SuppressMessage]`, `NoWarn`, ruleset, `DiagnosticSuppressor`, gerador
que emite supressão, arquivo que o analisador toma por código gerado e config que baixa a
severidade em massa. Sem política escrita, cada guarda cobria um analisador e deixava as outras
rotas abertas.

## Decisão

Em `core/src` nenhum analisador se suprime no código: nem `#pragma warning disable`, nem
`[SuppressMessage]` ou `[UnconditionalSuppressMessage]`, nem `NoWarn`, nem ruleset, nem marca de
código gerado. A única exceção é RS0030 nas classes `*LogMessages`, com `Justification`.

Uma regra de analisador só se desliga no `.editorconfig` da raiz, com o motivo escrito numa linha
de comentário logo acima: S1309, S1451 e S4055 em todo `*.cs`; qualquer outra só numa seção que
nomeia o arquivo (hoje S6585 no formatador de linha do registro). S4055 pede texto vindo de
`ResourceManager`, mas a decisão 0039 fixa um idioma só e tradução sem requisito seria YAGNI.

O compilador recebe só o `.editorconfig` da raiz e os configs do SDK. Os arquivos que o SDK gera
e os supressores que o SDK traz são exceções nomeadas, cada uma com o motivo, no teste.

## Recusados

- **Restringir a política aos analisadores do Sonar.** Pragma, ruleset e config calam qualquer
  analisador; só o Sonar ficaria guardado.
- **Supressão local com `Justification`.** O texto é livre e nada o confere; cada uso abre a
  porta do seguinte.
- **Exclusão por categoria ou em massa.** Desliga o que ninguém nomeou, e a categoria do Sonar
  tem espaço, que o compilador descarta.

## Como se impõe

O canário do log e o do Sonar plantam achados em cada tipo de cada fonte, de uma regra ligada no
`.editorconfig` e de uma ligada por padrão, e exigem o erro no build real. As regras da invocação
do compilador julgam os configs recebidos, a severidade efetiva por id e em massa, `generated_code`,
o ruleset, o analisador carregado e as opções de aviso. As da saída da build julgam os arquivos
gerados, que só valem na raiz da pasta intermediária, com o nome do projeto e a forma esperada, ou
vindos de um gerador nomeado, e os supressores. Cada rota tem uma fixture que a guarda recusa.

## Consequências

A causa se corrige no código, nunca se cala. O custo é o tempo da suíte de arquitetura, que passa a
compilar projetos de verdade, e uma regra do Sonar que não convém ao projeto precisa de decisão
escrita no `.editorconfig`, com motivo e escopo mínimo.

## O que reabre

Se uma regra de analisador sem alternativa de correção no código exigir supressão em três casos
seguidos, a política é revista por decisão escrita, nunca por exceção local.
