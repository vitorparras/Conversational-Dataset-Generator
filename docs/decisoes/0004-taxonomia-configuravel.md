# 0004 — A taxonomia é configuração, não código

> Decisão vigente. 2026-10-04.

## Decisão

As categorias de anotação vêm de um arquivo versionado junto ao projeto, carregado na inicialização.
Nenhuma categoria aparece como tipo no código, e nenhuma validação percorre uma lista fixa delas.

O arquivo declara, para cada categoria: identificador estável, rótulo exibido, definição, exemplos
do que inclui e do que não inclui, e se admite subcategorias. A carga falha se o arquivo estiver
ausente, malformado ou com identificador repetido — o sistema não sobe com taxonomia inválida.

Rótulos gravados são validados contra a taxonomia carregada, na escrita e na exportação.

## Por quê

A decisão 0001 estabelece que a ferramenta serve a mais de um domínio. Categoria escrita no código
inviabiliza isso de forma silenciosa: basta uma validação percorrer a lista fixa para que uma
instalação em outro domínio reprove na própria validação do pipeline, por não ter categorias que,
por definição, não existem ali.

Taxonomia em arquivo também é requisito de método. A definição de cada categoria é o que permite
medir concordância entre pessoas e justificar divergências. Ela precisa estar versionada e citável,
não dispersa em constantes.

## Recusados

| Opção | Por quê |
|---|---|
| Lista fixa no código | Impede outro domínio, e a falha aparece só na exportação |
| Taxonomia só em tabela do banco | Fica fora do controle de versão: não há como dizer sob qual taxonomia uma anotação foi feita |
| Sem validação de rótulo na escrita | Erro de digitação vira categoria nova em silêncio, e a medida de concordância passa a mentir |

## Consequências

O compilador deixa de verificar rótulos. A validação passa a ser de execução, feita no carregamento
e em cada escrita, e precisa de teste próprio.

A taxonomia vigente integra a identidade do dataset exportado: anotações feitas sob taxonomias
diferentes não são comparáveis, e a exportação registra sob qual delas foi produzida.

Mudar a taxonomia depois de haver anotação é mudança de dado, não de configuração. Exige decisão
própria sobre o que acontece com o que já foi anotado.

## O que reabre

Se um domínio exigir hierarquia mais profunda que categoria e subcategoria, a estrutura do arquivo
muda, e a decisão sobre o formato precisa ser revista.
