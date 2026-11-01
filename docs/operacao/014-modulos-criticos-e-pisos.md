# 014 — Módulos críticos e pisos de cobertura

> Operacional. 2026-10-12.

A [decisão 0007](../decisoes/0007-garantia-de-qualidade.md) manda a lista de módulos críticos e os
pisos de cobertura viverem aqui, porque mudam com o código. A verificação automática lê os números
dos arquivos da pasta [`areas/`](areas/): não existe segunda cópia em arquivo de configuração de
ferramenta. **Os números de uma área nova são proposta e aguardam aprovação do operador no pull
request que a introduz.**

## Critério de criticidade

Um módulo é crítico quando toca material, segredo, registro de operação, deslocamento de texto ou
esquema do banco: a falha nele vaza dado ou corrompe custódia, em vez de produzir tela torta.

## Áreas, pisos e motivos

Cada área é um arquivo de [`areas/`](areas/), nomeado pelo projeto ou pacote, com o nome da área
no título e quatro campos, um por linha, nesta ordem (o Motivo segue em linhas com dois espaços):

```
- Projeto/pacote: `Core.Platform.Keys`
- Piso: 90 %
- Crítica: sim
- Motivo: o que a área guarda ou decide e a decisão que a justifica
```

O piso é a porcentagem de linhas cobertas pela suíte da linguagem da área, ou `isento`. **Crítica**
diz `sim` ou `não` e é a única marca de módulo crítico: a mutação lê o campo, não o piso.
Projeto/pacote vale como **namespace e pasta**: o passo `floors` reprova o arquivo C# de `core/src`
que cai numa área pelo namespace e noutra pela pasta, nomeando as duas. No serviço, módulo e arquivo
coincidem. Código fora de toda área listada conta na do projeto ou pacote que o contém, pelo prefixo
mais longo. O Motivo da área "demais" lista após `Absorve:`, até o `;`, as pastas de primeiro nível
(módulos, no serviço) que ela cobre. O julgamento do `floors` está na
[019](019-leitura-da-tabela-pelo-floors.md). Área ainda sem projeto entra pelo nome, por exceção
declarada à [regra 5](../regras-de-escrita.md).

A visão de conjunto não é escrita à mão: o `floors` grava a tabela de todas as áreas, com piso e
medição, no resumo `by-area` da execução.

O teste de mutação percorre as áreas com `sim` em Crítica, pelas regras da
[017](017-teste-de-mutacao.md).

## Como um piso muda

Subir um piso ou acrescentar área muda o arquivo da área. Módulo novo que atende ao critério ganha o
seu arquivo no mesmo pull request que o cria: arquivo novo, que não conflita com o de outro pull
request. Baixar um piso exige decisão escrita em `docs/decisoes/`, com a justificativa e o que mudou
para que o valor anterior não valha mais. A decisão entra antes, em pull request próprio, nunca no
mesmo que faz a cobertura cruzar o piso para baixo.
