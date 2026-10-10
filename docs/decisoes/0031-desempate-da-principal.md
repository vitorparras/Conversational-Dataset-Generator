# 0031 — O desempate da categoria principal

> Decisão vigente. 2026-10-07. Completa a decisão 0022.

## Contexto

A [decisão 0022](0022-forma-do-rotulo.md) estabelece que o item recebe uma ou mais categorias e que
quem anota indica a principal, e registra que **empate real existe**: com duas categorias de mesmo
peso, a escolha é arbitrária, aparece como divergência, e "o protocolo precisa dizer como se decide,
ou a medida registra desacordo onde há concordância".

O efeito é concreto. Duas pessoas marcam o mesmo conjunto — concordância completa — e escolhem
principais diferentes entre categorias equivalentes. A medida da principal acusa divergência, e a
qualidade aparente do instrumento cai por causa de uma escolha que nunca teve resposta certa.

Convém lembrar o que a principal é: **a redução declarada para quem precisa de um rótulo só**, não
afirmação de que uma categoria importa mais. As categorias têm o mesmo peso entre si.

## Decisão

**Quem anota escolhe a principal**, e isso permanece: há casos em que uma categoria de fato
predomina, e quem lê o material inteiro percebe o que nenhuma regra derivaria.

**Quando quem anota declara empate, a principal é a primeira do conjunto na ordem da taxonomia.** A
ordem deixa de ser apresentação e passa a ser norma, declarada como tal na taxonomia do domínio.

**A anotação registra se a principal foi escolhida ou derivada do empate.** A medida de concordância
usa isso: duas principais derivadas são sempre iguais quando os conjuntos são iguais, e o par em que
uma foi escolhida e a outra derivada não se conta como divergência plena.

## Recusados

**Derivar sempre, sem quem anota escolher.** Acabaria com o empate e com o campo na tela, e perderia
o caso em que a predominância é real e perceptível — informação que nenhuma regra reconstrói.

**Deixar a principal vazia no empate.** Honesto sobre o dado, e devolve a quem usa o dataset o
problema que a principal existe para resolver: cada um reduziria de um jeito.

**Reduzir na exportação, sem principal na anotação.** Quem ignorasse a regra documentada voltaria a
reduzir do próprio jeito, e os resultados deixariam de ser comparáveis.

## Consequências

**Reordenar a taxonomia muda a principal de anotações anteriores** cuja principal foi derivada.
Isso entra no regime de mudança de taxonomia
([0016](0016-mudanca-de-taxonomia-com-anotacao-existente.md)), e a ordem passa a ser parte
versionada da taxonomia, não detalhe de exibição.

A tela ganha uma forma de declarar empate, distinta de escolher uma das categorias. Declarar empate
não é deixar o campo em branco: é uma afirmação sobre o material.

A medida da principal passa a ter três casos, não um: escolhida contra escolhida, derivada contra
derivada, e escolhida contra derivada.
