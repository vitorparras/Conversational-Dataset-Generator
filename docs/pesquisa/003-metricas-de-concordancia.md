# 003 — Métricas de concordância

> Descrição. 2026-10-04.

Nenhuma medida isolada descreve a concordância de uma campanha. Cada uma responde a uma pergunta
estreita, e o conjunto é informativo porque uma cobre o que a outra deixa passar. Aqui estão as que
o projeto calcula, com o que cada uma mede e o que ela não mede; como se lê o resultado está em
[004](004-leitura-da-concordancia.md).

## Sobre o quê

Toda medida é calculada por domínio, por campanha, por versão da taxonomia e por forma da unidade
de análise — misturar formas mistura tarefas diferentes. Além do número global, por categoria: um
valor alto no conjunto esconde categoria que ninguém consegue aplicar.

Que toda medida declare a base sobre a qual foi calculada é exigência do requisito de
[concordância comparável](../requisitos/013-concordancia-comparavel.md), e medir **antes da
arbitragem**, sobre as duas anotações originais, é a
[decisão 0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md). Daqui sai o resto: a medida é
publicada junto da proporção em dupla anotação e da contagem de itens marcados como não decidíveis.

## As medidas

**Concordância observada** — proporção de itens em que as pessoas deram o mesmo rótulo. Responde
quantas vezes coincidiram. Não responde se coincidir era fácil: num conjunto dominado por uma
categoria, concordância alta é o que se espera até de quem escolhe no escuro.

**Coeficiente kappa** — concordância observada descontada da que se esperaria se cada pessoa
distribuísse rótulos ao acaso, respeitando a frequência com que de fato usou cada um. Definido para
duas pessoas e categorias sem ordem entre si. Responde quanto da coincidência está além do acaso.
Não responde nada estável quando uma categoria domina: o termo de acaso cresce e o coeficiente cai
mesmo com coincidência alta. Também não trata mais de dois anotadores, nem item que alguém deixou
em branco.

**Coeficiente alfa** — razão entre a discordância observada e a que se esperaria se os rótulos
fossem atribuídos ao acaso sobre a distribuição reunida de todos. Admite qualquer número de
anotadores, itens sem anotação de parte deles, e uma função de distância entre categorias, de modo
que confundir duas categorias próximas pese menos que confundir duas distantes. Responde pela
concordância sob o desenho real da campanha. Não responde onde está o problema: é um número só.

**Concordância por categoria, com matriz de confusão** — para cada categoria, com que frequência
quem a usou foi acompanhado, e contra o que ela é trocada. Responde onde a taxonomia falha. Não
responde pela qualidade do conjunto.

**Concordância sobre trechos** — na forma em que a anotação recai sobre trechos, calculada com a
tolerância de fronteira que a campanha declarou e publicada junto dela. Responde se as pessoas
marcam o mesmo pedaço. Não responde se o pedaço é o certo.

**Concordância de uma pessoa consigo mesma** — sobre os itens reapresentados depois de um
intervalo. Responde se o limite é a taxonomia ou a consistência de quem anota.
