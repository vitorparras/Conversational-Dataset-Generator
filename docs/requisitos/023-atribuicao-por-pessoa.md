# 023 — A atribuição é por pessoa, e o item sabe quem falta

> Norma vigente. 2026-10-04.

**A lista de trabalho de uma pessoa é determinada pelo que ela mesma já decidiu, um item pode estar
atribuído a mais de uma pessoa ao mesmo tempo, e o estado do item separa o que está fechado do que
já tem uma decisão e espera outra.**

## Por que existe

Sem esta garantia, [011](011-anotacoes-independentes.md) e [012](012-divergencia-arbitrada.md) não
são satisfazíveis. Uma lista de trabalho que exclui o que qualquer pessoa já decidiu nunca entrega o
mesmo item à segunda, e então não há duas anotações para serem independentes, nem divergência para
arbitrar — e tampouco itens com mais de uma anotação para [013](013-concordancia-comparavel.md)
medir.

A diferença entre "o que eu decidi" e "o que alguém decidiu" é invisível enquanto houver um anotador
só, e decide tudo no instante em que houver dois. É o tipo de defeito que não aparece em teste feito
com uma pessoa.

O estado intermediário existe pelo mesmo motivo: sem ele, decidido é ambíguo, e um item com uma
decisão de duas conta como pronto. A campanha então se declara concluída com metade da sobreposição
que prometeu — e a concordância é medida sobre o que sobrou.

## Critérios de aceitação

- Dado um item já decidido por uma pessoa, quando ele é atribuído a uma segunda, então a atribuição
  coexiste com a primeira em vez de substituí-la.
- Dado um item que a própria pessoa já decidiu, quando ela pede sua lista de trabalho, então ele não
  aparece; dado um item decidido só por outra pessoa, então aparece.
- Dado um item com uma decisão de duas, quando se pergunta seu estado, então a resposta o distingue
  de não iniciado e de fechado, e diz quantas decisões faltam.
- Dado uma contagem de progresso, quando ela é exibida ou exportada, então item que ainda espera
  decisão não entra como concluído.
- Dado uma pessoa sem concessão para o domínio do item, quando se tenta atribuí-lo a ela, então a
  atribuição é recusada: atribuir não concede acesso, conforme
  [017](017-acesso-negado-por-padrao.md).

## O que não exige

Não exige quantas pessoas anotam cada item, nem que a sobreposição cubra o acervo inteiro: isso é
escolha de campanha, e a garantia vale para a escolha que for feita. Não exige ordem específica na
lista, nem que a atribuição seja manual ou automática. Não exige impedir reatribuição — exige que,
feita, a lista reflita. Não exige um conjunto fixo de resultados possíveis: qualquer decisão
registrada por aquela pessoa, inclusive a de não conseguir decidir, a tira da lista dela.
