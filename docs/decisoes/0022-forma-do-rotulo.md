# 0022 — A forma do rótulo: duas dimensões, com principal

> Decisão vigente. 2026-10-07.

## Contexto

Material conversacional real não se encaixa numa categoria só. Uma conversa em que alguém negocia
uma venda ilícita e ameaça quem não pagou é as duas coisas, e obrigar a escolher uma esconde metade
do que aconteceu.

Há também características que acompanham qualquer categoria sem se confundir com nenhuma: grau de
toxicidade, discurso de ódio, informação falsa. Dizer que uma conversa "é tráfico" e que ela "tem
alta toxicidade" são afirmações de natureza diferente — a primeira diz o que o material é, a
segunda como ele é. Nenhuma decisão vigente tratava disso, e o modelo assumia rótulo único por
implicação.

## Decisão

**A anotação tem duas dimensões independentes.**

**Categorias** dizem o que o material é. Um item recebe **uma ou mais**, e quem anota indica qual é
a **principal** — a predominante. As demais ficam registradas sem ordem entre si.

**Dimensões** dizem como o material é, e acompanham qualquer categoria. Cada uma tem seu próprio
conjunto de valores possíveis, declarado na configuração do domínio junto com a taxonomia.

Cada eixo tem vocabulário próprio, medida de concordância própria e tratamento próprio na
exportação. Não se misturam numa lista só.

## Recusados

**Rótulo único.** Falso sobre o material: forçaria escolher entre coisas que coexistem, e a escolha
variaria por pessoa, aparecendo como divergência onde não há.

**Categorias sem principal.** Joga para quem usa o dataset a decisão de como reduzir a rótulo único
— e cada um reduziria de um jeito, tornando resultados incomparáveis.

**Dimensões como categorias.** Tornaria a concordância ilegível: discordar sobre se houve tráfico é
diferente de discordar sobre grau de toxicidade, e um número só mistura as duas perguntas.

## Consequências

**A medida de concordância muda.** O coeficiente definido para um rótulo por item não serve para
conjuntos: é preciso medida que trate concordância parcial — quem marcou três categorias iguais e
uma diferente não discordou por inteiro. A principal é medida à parte, como rótulo único.

A tela precisa permitir escolher várias e indicar a principal, o que interage com os atalhos de
teclado. A exportação declara os dois eixos, e quem recebe precisa saber que a principal é uma
redução, não a única verdade sobre o item.

**Empate real existe.** Com duas categorias de mesmo peso, a escolha da principal é arbitrária e
aparecerá como divergência. O protocolo precisa dizer como se decide, ou a medida registra desacordo
onde há concordância.

## O que reabre

Domínio sem característica que valha como dimensão declara o eixo vazio — não é motivo para rever.

Se a divergência na escolha da principal dominar a medida, torná-la opcional volta à mesa, ao custo
de distinguir ausência de predominância de ausência de preenchimento.
