# 017 — O teste de mutação

> Operacional. 2026-10-26.

O teste de mutação altera o código de um módulo crítico e verifica se algum teste reclama. A lista
de módulos críticos é o campo **Crítica** dos arquivos de áreas da
[014](014-modulos-criticos-e-pisos.md); o piso de cobertura não a define.

## Regras

Este documento é lido pela verificação: a seção `## Regras` e as três frases em negrito abaixo, a
de sobreviventes, a do teto de tempo e a do argumento de `ConfigureAwait`. Mudança nelas segue o
mesmo formato.

A mutação roda sobre as áreas com `sim` em Crítica. Cada arquivo pertence a uma só área, a de
prefixo mais longo, como na cobertura: área aninhada, crítica ou não, sai da área que a contém, e
nenhum arquivo conta duas vezes.

Por módulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: **20 minutos
por módulo**, cada módulo no seu job da verificação de pull request, sem somar com os outros: a
matriz do fluxo lê as áreas da 014 e abre um job por módulo crítico, e um job agregado, com o nome
"Mutação nos módulos críticos", só passa se todos eles passarem. Exceder o teto é achado que
reprova, nomeando o módulo e o tempo gasto, nunca lentidão tolerada. O tempo de cada módulo sai no
relatório do passo. Mutação fora do bloqueio só por decisão escrita.

No núcleo, em todos os módulos, a verificação não conta **o mutante `false`→`true` do argumento de
`ConfigureAwait`**: o host não tem contexto de sincronização, então esse mutante é equivalente por
construção e não entra como gerado nem como sobrevivente. A regra vale só para ele. As demais
mutações de uma linha que chama `ConfigureAwait`, como a remoção da instrução ou do bloco que a
contém, contam como quaisquer outras.

## Qual suíte conta

Em cada módulo conta só a suíte do projeto dele, nas duas linguagens. Módulo sem teste assim
reprova. O sobrevivente que só teste de outro projeto mataria aparece como sobrevivente, e o módulo
responde por ele com teste próprio.

- **Núcleo:** o projeto de teste da área, se ela tem um próprio (`<Área>.Tests`, como
  `Core.Persistence.Migrations.Tests`), senão o do projeto do módulo (`<Projeto>.Tests`, ou o do
  projeto em que ele está aninhado, como `Core.Boundary.Tests` para `Core.Boundary.Contracts`),
  desde que referencie o projeto do módulo. As suítes transversais (`Core.Tests.Integration`,
  `Core.Tests.Properties`) não contam. Teste novo de banco entra no projeto da área que ele exercita
  (`Core.Persistence.Migrations.Tests`, `.Content.Tests`, `.Custody.Chain.Tests` ou
  `.ServerGuarantees.Tests`); o resto fica em `Core.Persistence.Tests`. O apoio comum (contêiner,
  `ScratchDatabase`) mora em `Core.Tests.Database`, que não é projeto de teste.
  O Stryker.NET roda com um worker em todos os módulos: o veredito de um mutante em inicializador
  estático muda com o número de workers, e o resultado tem de ser o mesmo em duas rodadas. Nenhum
  mutante sai da conta por ser estático.
  O Stryker.NET reinicia o servidor de teste a cada mutante que estoura o timeout. Quando o projeto
  de teste referencia o Testcontainers, o passo entrega o token na variável
  `CDG_TEST_DATABASE_REUSE` e o projeto mantém uma imagem e um contêiner nomeados por ele, que
  sobrevivem aos reinícios; o passo os remove ao fim, também se o Stryker falha ou passa do teto.
  Sem a variável, cada execução sobe e descarta o seu Postgres; sem o `docker` no PATH, nada é
  reaproveitado. Se o processo da verificação morrer à força, o `finally` não roda e o contêiner
  `cdg-test-database-<token>` e a imagem `cdg-test-database:<token>` ficam: ache-os com
  `docker ps -a --filter name=cdg-test-database-` e remova com `docker rm --force --volumes` e
  `docker rmi --force`.
- **Serviço:** os arquivos de teste que importam o módulo diretamente (`import language_service.x`,
  `from language_service.x import ...` ou `from language_service import x`), numa rodada do mutmut
  por módulo. Os testes que leem o texto do código-fonte ficam de fora: a mutação o reescreve.
