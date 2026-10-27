# 017 — O teste de mutação

> Operacional. 2026-10-26.

O teste de mutação altera o código de um módulo crítico e verifica se algum teste reclama. A lista
de módulos críticos é a coluna **Crítica** da tabela da
[014](014-modulos-criticos-e-pisos.md); o piso de cobertura não a define.

## Regras

Este documento é lido pela verificação: a seção `## Regras` e as duas frases em negrito abaixo, a
de sobreviventes e a do teto de tempo. Mudança nelas segue o mesmo formato.

A mutação roda sobre as áreas com `sim` em Crítica. Cada arquivo pertence a uma só área, a de
prefixo mais longo, como na cobertura: área aninhada, crítica ou não, sai da área que a contém, e
nenhum arquivo conta duas vezes.

Por módulo, no máximo **20 % dos mutantes gerados** podem sobreviver. Teto de tempo: **20 minutos
por linguagem** dentro da verificação de pull request, somados entre os módulos da linguagem.
Exceder o teto é achado que reprova, nomeando o módulo e o tempo gasto, nunca lentidão tolerada.
Mutação fora do bloqueio só por decisão escrita.

No núcleo, o Stryker ignora as chamadas a `ConfigureAwait` em todos os módulos: o host não tem
contexto de sincronização, então os mutantes dessas chamadas são equivalentes por construção e não
contam como gerados nem como sobreviventes. Nenhuma outra chamada é ignorada.

## Qual suíte conta

Em cada módulo conta só a suíte do projeto dele, nas duas linguagens. Módulo sem teste assim
reprova. O sobrevivente que só teste de outro projeto mataria aparece como sobrevivente, e o módulo
responde por ele com teste próprio.

- **Núcleo:** os projetos de teste que referenciam o projeto do módulo.
- **Serviço:** os arquivos de teste que importam o módulo diretamente (`import language_service.x`,
  `from language_service.x import ...` ou `from language_service import x`), numa rodada do mutmut
  por módulo. Os testes que leem o texto do código-fonte ficam de fora: a mutação o reescreve.
