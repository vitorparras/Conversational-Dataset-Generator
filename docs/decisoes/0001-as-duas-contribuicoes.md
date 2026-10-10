# 0001 — As duas contribuições do projeto

> Decisão vigente. 2026-10-04.

## Contexto

O projeto produz duas coisas: um dataset conversacional anotado em português brasileiro, no domínio
forense, e a ferramenta que o constrói. Elas vão disputar a mesma decisão de design com frequência,
e é melhor ter a resposta escrita antes da primeira disputa.

O caso típico: tirar a lista de categorias do código e pô-la num arquivo de configuração melhora a
ferramenta e não faz nada pelo dataset, que tem as categorias que tem. Vale o esforço ou não?

## Decisão

**As duas são contribuições.** A ferramenta não é meio para chegar ao dataset, nem o dataset é
pretexto para justificar a ferramenta.

O dataset vale porque recursos anotados em português brasileiro são escassos, e no domínio forense
praticamente inexistentes. A ferramenta vale porque o método de construir uma base assim é o que se
transfere para outros domínios — e porque dados envelhecem enquanto o método não.

Quando as duas disputarem uma decisão, a pergunta é: **isso prejudica alguma das duas?** Se
prejudica o dataset, não entra. Se prejudica a ferramenta, precisa de justificativa escrita. Se
beneficia só uma sem atrapalhar a outra, entra normalmente.

## Recusados

| Opção | Por quê |
|---|---|
| Só o dataset é contribuição | Quem ler a norma conclui, corretamente, que generalidade é desperdício — e age assim |
| Só a ferramenta é contribuição | Descarta valor real; o dataset consome a maior parte do esforço |
| Duas linhas de trabalho paralelas | Duplica esforço e cria duas descrições do mesmo pipeline, que divergem |

## Consequências

A lista de categorias do domínio não pode estar escrita no código. Se as categorias forem um tipo
usado nas assinaturas das funções, ou se alguma validação percorrer essa lista para liberar a
exportação, uma instalação em outro domínio não passa na própria validação do pipeline.

Relatórios e métricas passam a fazer parte do produto: a ferramenta precisa saber comparar a
qualidade da anotação entre bases diferentes.

Em troca, há mais superfície para manter, e o custo de construção aumenta.

Contra o risco de generalidade especulativa, o contrapeso é: **generalidade se justifica por um
segundo caso concreto, nunca por hipótese.**

## O que reabre

Se a revisão por pares não aceitar as duas entregas como contribuições.

Se nenhum segundo domínio aparecer até o início da anotação — a flexibilidade para onde está.

Se a capacidade de execução não comportar as duas: entrega-se o dataset, e a generalidade vira
trabalho futuro declarado, por decisão que substitua esta.
