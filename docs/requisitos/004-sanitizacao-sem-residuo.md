# 004 — A sanitização não deixa resíduo

> Norma vigente. 2026-10-04.

**Nenhum texto sai da sanitização contendo o que a sanitização deveria ter removido.**

## Por que existe

A [decisão 0007](../decisoes/0007-garantia-de-qualidade.md) cita este invariante como o exemplo de
afirmação que vale para todo texto possível, e é por isso que ele é requisito e não detalhe de
implementação: exemplo escolhido à mão testa apenas os casos em que quem escreveu o teste pensou.

Resíduo é a falha mais cara do sistema porque é silenciosa. Ele atravessa a anotação, onde ninguém
está procurando por ele, e chega à exportação com aparência de texto limpo. As camadas seguintes —
[015](015-exportacao-sem-identidade.md) — existem justamente porque esta pode errar.

## Critérios de aceitação

- Dado qualquer texto de entrada, quando ele é sanitizado, então o resultado não contém nenhuma das
  formas que a política manda remover, em nenhuma variação de grafia, separador ou espaçamento que
  ainda seja reconhecível.
- Dado um texto em que a forma a remover aparece partida por caractere invisível ou separador
  incomum, quando ele é sanitizado, então ela é removida igualmente — partir não é esconder.
- Dado um texto já sanitizado, quando ele é sanitizado outra vez, então o resultado é idêntico.
- Dado um texto em que nada havia a remover, quando ele é sanitizado, então ele sai inalterado.

A prova é uma propriedade sobre todo texto gerado, e não uma lista de exemplos, prevista em
testes/propriedades/sanitizacao e ainda inexistente.

## O que não exige

Não exige que a sanitização decida sozinha o que é nome de pessoa: reconhecimento de nome é
imperfeito por natureza e entra como advertência na auditoria de exportação, não como garantia
aqui. Não exige remover o que não identifica ninguém — o que ela deve conservar está em
[005](005-sanitizacao-preserva-o-sinal.md).
