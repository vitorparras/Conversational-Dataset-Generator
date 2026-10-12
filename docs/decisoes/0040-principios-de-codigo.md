# 0040 — Princípios de código antes do código

> Decisão vigente. 2026-10-12.

## Contexto

O primeiro ciclo de código da Fase 1 produziu três pull requests sem padrão de código acordado:
cada agente escreveu no seu estilo, uma classe chegou a 435 linhas com seis responsabilidades, o
ponto de entrada montava os objetos à mão, membros públicos existiam só para teste, e um pull
request por etapa somou mais de cem arquivos. Nada disso era defeito funcional: os testes passavam.
Era código que o operador não conseguia revisar e não queria manter. Os três foram fechados sem
mesclar, e a branch de cada um ficou como referência.

## Decisão

Nenhuma linha de código entra sem seguir as [regras de código](../regras-de-codigo.md): princípios
(SOLID, DRY tardio, YAGNI, composição sobre herança, Clean Code), limites numéricos, raiz de
composição única, proibição de gancho de teste, regime de erro, comentário mínimo e teto de pull
request. O que é mensurável é imposto por mecanismo; o resto, por lista de revisão respondida por
escrito. DDD fica para a Fase 2, quando houver domínio a modelar.

## Recusados

| Opção | Por quê |
|---|---|
| Sem padrão, confiando na revisão | Foi o que aconteceu: revisão de cem arquivos não acontece |
| Padrão só em prosa, sem mecanismo | Entre agentes diferentes, cada um lê a prosa do seu jeito |
| Só limites numéricos, sem princípio | Classe pequena com responsabilidade errada passa no número |
| DDD desde a Fase 1 | Não há domínio na fundação; viraria cerimônia sobre infraestrutura |
| Contêiner de injeção de terceiro | A injeção nativa da plataforma basta e não traz convenção oculta |
| Um pull request por etapa | Irrevisável; a unidade de revisão é a task |

## Consequências

O código da Fase 1 é refeito sob as regras, em pull requests pequenos. Um teste de arquitetura faz
parte da fundação desde o primeiro pull request, e um revisor aplica a lista antes de cada abertura.
O padrão custa mais arquivos e mais tipos pequenos; é o preço de cada arquivo caber numa revisão.

## O que reabre

Se os limites numéricos reprovarem código que o operador, lendo, considerar correto e legível em
três casos seguidos, os números são revistos por decisão escrita, nunca por exceção local.
