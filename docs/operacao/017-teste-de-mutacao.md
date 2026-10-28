# 017 — O teste de mutação

> Operacional. 2026-10-26.

O teste de mutação altera o código de um módulo crítico e verifica se algum teste reclama. A lista
de módulos críticos é a coluna **Crítica** da tabela da
[014](014-modulos-criticos-e-pisos.md); o piso de cobertura não a define.

## Regras

Este documento é lido pela verificação: a seção `## Regras` e as três frases em negrito abaixo, a
de sobreviventes, a do teto de tempo e a do argumento de `ConfigureAwait`. Mudança nelas segue o
mesmo formato.

A mutação roda sobre as áreas com `sim` em Crítica. Cada arquivo pertence a uma só área, a de
prefixo mais longo, como na cobertura: área aninhada, crítica ou não, sai da área que a contém, e
nenhum arquivo conta duas vezes.

Por módulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: **20 minutos
por linguagem** dentro da verificação de pull request, somados entre os módulos da linguagem.
Exceder o teto é achado que reprova, nomeando o módulo e o tempo gasto, nunca lentidão tolerada.
Mutação fora do bloqueio só por decisão escrita.

No núcleo, em todos os módulos, a verificação não conta **o mutante `false`→`true` do argumento de
`ConfigureAwait`**: o host não tem contexto de sincronização, então esse mutante é equivalente por
construção e não entra como gerado nem como sobrevivente. A regra vale só para ele. As demais
mutações de uma linha que chama `ConfigureAwait`, como a remoção da instrução ou do bloco que a
contém, contam como quaisquer outras.

No núcleo, o mutante de inicializador estático (o Stryker.NET o marca como `static` no relatório)
não entra como gerado nem como sobrevivente: o valor se fixa quando o tipo é inicializado, e o
veredito do Stryker.NET (runner `mtp`) depende do estado do processo de teste, então muda com o
número de workers sem que nenhum teste mude. A verificação o nomeia, com o lugar, na linha "fora da
conta" do módulo, para que continue visível. Mutante sem veredito, como o que não compila, segue
fora da conta como antes.

## Qual suíte conta

Em cada módulo conta só a suíte do projeto dele, nas duas linguagens. Módulo sem teste assim
reprova. O sobrevivente que só teste de outro projeto mataria aparece como sobrevivente, e o módulo
responde por ele com teste próprio.

- **Núcleo:** o projeto de teste do projeto do módulo (`<Projeto>.Tests`, ou o do projeto em que ele
  está aninhado, como `Core.Boundary.Tests` para `Core.Boundary.Contracts`), desde que o referencie.
  As suítes transversais (`Core.Tests.Integration`, `Core.Tests.Properties`) não contam, mesmo
  referenciando o módulo.
  O Stryker.NET roda com dois workers no módulo cujo projeto de teste não sobe o banco, e com um
  quando esse projeto usa Testcontainers: cada worker subiria o seu contêiner.
- **Serviço:** os arquivos de teste que importam o módulo diretamente (`import language_service.x`,
  `from language_service.x import ...` ou `from language_service import x`), numa rodada do mutmut
  por módulo. Os testes que leem o texto do código-fonte ficam de fora: a mutação o reescreve.
