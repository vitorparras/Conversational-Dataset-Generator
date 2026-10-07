# 0023 — Suficiência do recorte exportado

> Decisão vigente. 2026-10-07.

## Contexto

As condições para exportar tratam de procedência, integridade e ausência de resíduo. Nenhuma trata
de **quanto** há, nem de **como está distribuído**.

Um recorte com três exemplos numa categoria passa em todas elas: sai com métricas recalculáveis,
procedência declarada e nenhum dado pessoal. E não serve para nada — quem recebe só descobre ao
tentar usar. O dataset é uma das duas contribuições do projeto, e entregar um que atende a todo
critério de forma e a nenhum de substância contradiz essa posição.

## Decisão

**A exportação verifica suficiência antes de produzir o artefato, e recusa quando o recorte não a
atende.**

São três perguntas, e cada uma tem limite declarado na **configuração do domínio**, ao lado da
taxonomia:

Quantos itens no total. Quantos itens por categoria — avaliado em cada categoria declarada, não só
na principal. E quão desigual é a distribuição entre categorias.

O limite é do domínio, porque um com cinco categorias e outro com quarenta não têm o mesmo piso
razoável. **Não há valor embutido no sistema**: domínio sem limites declarados não exporta, em vez
de exportar sob régua inventada. O artefato registra quais limites valiam e os valores medidos.

## Recusados

**Avisar sem recusar.** Aviso em exportação é ignorado — quem exporta quer o arquivo, e ele sai de
qualquer jeito. Recusa é o que faz a régua existir.

**Pisos fixos no sistema.** Arbitrários para qualquer domínio que não fosse o primeiro, e
reproduziriam no número o acoplamento que a taxonomia configurável desfez.

**Valor padrão ajustável.** Funciona sem configurar, e é justamente o problema: o padrão vira a
escolha de quem não pensou no assunto, e passa a ser citado como se fosse critério.

**Medir só a categoria principal.** Uma categoria que nunca é principal mas aparece em muitos itens
ficaria invisível na contagem, e sumiria do dataset sem que nada acusasse.

## Consequências

Um domínio precisa declarar os limites antes da primeira exportação, e isso exige pensar sobre o
tamanho útil do recorte antes de ter o recorte.

Campanha exploratória, com pouco material por desenho, não exporta sob os limites da campanha
definitiva. Declarar limites próprios para ela é legítimo, e fica registrado no artefato.

A medida de desigualdade entre categorias depende da forma do rótulo
([0022](0022-forma-do-rotulo.md)): com item contando em mais de uma categoria, a soma das contagens
excede o total de itens, e isso precisa estar dito em quem calcular.

## O que reabre

Se um domínio exigir exportação de recorte propositalmente pequeno — um conjunto de exemplos para
ilustrar uma categoria rara, por exemplo —, a recusa vira obstáculo, e a decisão precisa de uma
saída declarada em vez de ser contornada.
