# AGENTS.md

Ponto de entrada único do repositório. Vale para pessoas e para agentes de IA. Arquivos específicos
de ferramenta apenas redirecionam para cá e não contêm instruções próprias — duas cópias divergem.

> Documentação e código vivem aqui. Texto segue `docs/regras-de-escrita.md`; código segue
> `docs/regras-de-codigo.md`. Toda linha de código depende das decisões registradas aqui.

## O que é o projeto

Uma ferramenta para construir bases conversacionais anotadas em português brasileiro: ingestão,
sanitização, pseudonimização que preserva contexto, anotação humana assistida, validação por
métricas de concordância e exportação rastreável. O domínio forense é o caso mais exigente.

A ferramenta e o dataset são **as duas contribuições** do projeto, e nenhuma é meio para a outra.

## Hierarquia de autoridade

Quando duas fontes discordam, a de cima ganha.

| # | Fonte | Autoridade |
|---|---|---|
| 1 | `docs/decisoes/` com status `vigente` | Máxima. Contrariar exige substituir por outra decisão |
| 2 | `docs/requisitos/` e `docs/privacidade/` | Vinculante |
| 3 | `docs/roadmap/` | Vincula a ordem de execução |
| 4 | `docs/arquitetura/`, `docs/operacao/`, `docs/pesquisa/` | Descrevem. Não mandam |
| 5 | `docs/historico/` | Autoridade zero. Nunca é norma |

Duas decisões vigentes em contradição são **defeito a resolver por substituição**, não hierarquia a
interpretar. Nenhuma vence por ser mais recente.

**Divergência não se resolve sozinha.** Pare, cite os dois arquivos, peça decisão.

## Regras invioláveis

**Zero dado pessoal, segredo ou caminho de máquina** em qualquer arquivo — inclusive exemplo e
teste. Exemplos usam identificadores sintéticos, nunca nomes plausíveis. É a regra sem discussão.

**Se você não leu, você não sabe.** Afirmação sobre documento ou código é rastreável a arquivo
aberto nesta execução, citado por caminho. Número afirmado é número contado.

**Nada de fora do repositório.** Não cite projeto, prazo, medição ou documento que quem clona não
possa consultar. Argumento que precisa de referência externa não se sustenta.

**Merge é do operador.** Agente commita em branch própria e abre PR; nunca force, reset ou merge.

**Na dúvida entre assumir e perguntar, pergunte.** Silêncio não é aprovação.

## Antes de escrever

Texto: `docs/regras-de-escrita.md`, imposta por `tools/verify-docs/`; rode antes de propor mudança.
Código: `docs/regras-de-codigo.md`; o mensurável reprova o build, o resto é lista de revisão.

## O que reprova um trabalho

Afirmar sem ter aberto o arquivo · contrariar decisão vigente sem substituí-la · dado pessoal ou
segredo em qualquer lugar · duplicar fato que já vive em outro documento · citar arquivo ou
identificador inexistente · prometer privacidade sem nomear o teste que a prova · resolver
divergência por conta própria · documento acima de 60 linhas · código fora das regras de código ·
declarar pronto sem a verificação que comprova.
