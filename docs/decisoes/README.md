# Decisões

> Descrição. Índice e formato das decisões.

Cada decisão é um arquivo. **Mudar de ideia é escrever um arquivo novo que substitui o antigo** —
nunca editar o texto do antigo. O substituído permanece, com a linha de autoridade atualizada.

Isso preserva o histórico de cada mudança de rumo, que é o que demonstra que cada virada foi
deliberada e justificada.

## Índice

| # | Decisão | Autoridade |
|---|---|---|
| [0001](0001-as-duas-contribuicoes.md) | As duas contribuições do projeto | Decisão vigente |
| [0002](0002-stack.md) | A stack | Decisão vigente |
| [0003](0003-cifra-em-repouso.md) | Cifra em repouso | Decisão vigente |
| [0004](0004-taxonomia-configuravel.md) | A taxonomia é configuração, não código | Decisão vigente |
| [0005](0005-unidades-de-analise.md) | As unidades de análise | Decisão vigente |
| [0006](0006-dominios-coexistem.md) | Domínios coexistem; a exportação é que recorta | Decisão vigente |
| [0007](0007-garantia-de-qualidade.md) | A garantia de qualidade dos testes | Decisão vigente |

## Formato

Nome `NNNN-slug.md`. Autoridade `Decisão vigente`. Seções:

1. **Contexto** — o que torna a decisão necessária, com a origem citada
2. **Decisão** — uma frase imperativa
3. **Alternativas recusadas** — cada uma com o motivo da recusa
4. **Consequências** — o que passa a ser verdade, **incluindo o que piora**
5. **O que reabre esta decisão** — a condição que, se acontecer, obriga reavaliar

A seção 5 é a que mais falta na prática e a que mais importa num projeto longo: transforma "mudei
de ideia" em "a condição que eu mesmo escrevi se realizou".

Ver [regras de escrita](../regras-de-escrita.md).
