# 0038 — A marca da entrega fica no manifesto

> Decisão vigente. 2026-10-07. Completa o requisito 034.

## Contexto

O [requisito 034](../requisitos/034-copia-vazada-e-atribuivel.md) exige que cada entrega seja
distinguível das demais, para que uma cópia encontrada fora possa ser ligada a quem a recebeu. E
exige que o conteúdo analisável permaneça idêntico entre entregas do mesmo recorte, porque é isso
que torna o resultado reproduzível.

As duas exigências parecem colidir. Não colidem — mas só se a marca ficar fora do conteúdo
analisável, e nenhum documento dizia onde.

## Decisão

**A marca fica no manifesto do artefato**, ao lado do que ele já declara: domínio, taxonomia,
recorte, unidade do deslocamento e como cada medida contou o conteúdo replicado.

O conteúdo analisável permanece idêntico, byte a byte, entre duas entregas do mesmo recorte. Quem
recalcular uma medida sobre duas entregas diferentes chega ao mesmo número.

A cadeia registra a quem, quando, sob qual contrato e com qual marca
([requisito 034](../requisitos/034-copia-vazada-e-atribuivel.md)).

## Recusados

**Marca na ordem dos itens.** Cada entrega embaralharia os itens de um jeito próprio, e a marca
sobreviveria a quem não soubesse que existe. E o arquivo deixaria de ser idêntico entre entregas: a
reprodutibilidade passaria a exigir reordenar antes de comparar, que é um passo que quem for
conferir pode não saber que precisa dar — e cuja ausência produz divergência que parece erro de
medida.

**Registro interno, nada no artefato.** O sistema saberia o que foi entregue a quem, e o artefato
sairia limpo. Uma cópia encontrada em circulação não apontaria para ninguém, que é exatamente o
problema que o requisito existe para resolver.

## Consequências

**A marca não resiste a quem a procure.** Apagar uma linha do manifesto a remove, e o requisito 034
já declara isso: ela identifica entrega, não impede repasse deliberado de quem conhece o mecanismo.

O que ela sustenta é o caso comum — cópia que circula por descuido, repasse de quem não pensou no
assunto, artefato encontrado onde não devia estar. Para o caso deliberado, o que existe é o
contrato, e ele é instrumento jurídico, não técnico.
