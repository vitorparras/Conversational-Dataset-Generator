# 007 — Reprodutibilidade

> Operacional. 2026-10-04.

Reproduzir, aqui, é outra pessoa repetir o procedimento e comparar resultados — não necessariamente
sobre o mesmo material, porque parte do material não pode sair. O que se exige é que nada do que
separa um número do outro fique implícito.

Que quem recebe o dataset recalcule as métricas publicadas e chegue aos mesmos números é o
requisito de [métricas recalculáveis](../requisitos/016-metricas-recalculaveis.md). O que segue é o
que precisa viajar para que isso seja possível.

## O que acompanha cada exportação

- A taxonomia **inteira**, com a definição e os exemplos de cada categoria, e o número da versão: a
  [decisão 0015](../decisoes/0015-versionamento-e-compatibilidade.md) exige artefato
  autossuficiente, e taxonomia por referência deixa de ser consultável quando o arquivo mudar
- A declaração da campanha: domínio, forma da unidade, quantas pessoas por item, proporção sob
  sobreposição, regra de arbitragem, regime de sugestão, critério de parada
- As medidas calculadas, com o escopo e o denominador de cada uma
- As contagens: itens, anotadores, exclusões com motivo, itens não decidíveis
- O resultado da verificação de custódia no período
- As versões sob as quais o artefato foi produzido: pipeline, modelo e esquema de exportação

## Os anotadores aparecem sem nome

Cada pessoa recebe um identificador estável dentro da campanha, e é por ele que aparece em qualquer
número publicado. Assim outra pessoa recalcula a concordância por anotador sem saber de quem se
trata. Nome de pessoa não entra em artefato nenhum deste projeto.

## O mesmo resultado duas vezes

A mesma entrada, na mesma versão e com a mesma configuração, produz a mesma exportação — igual byte
a byte, salvo o horário declarado. Qualquer outra variação é achado, não tolerância.

Onde há sorteio — a parte sob dupla anotação, a ordem dos itens por pessoa —, a semente fica
registrada. Sorteio que não se re-deriva torna impossível conferir a concordância publicada.

## O que não se reproduz, e o que entra no lugar

O texto bruto e o mapa de reversão nunca saem, por força da
[decisão 0003](../decisoes/0003-cifra-em-repouso.md). A sanitização, então, não é reproduzível a
partir do original: o que se reproduz é que a mesma entrada atravessa a mesma versão e sai igual.
Para isso o projeto mantém material sintético que exercita cada regra de sanitização, e é esse
material que viaja com a exportação.

Quando o material anotado também não pode ser publicado, o artefato declara o que ficou retido. A
comparação passa a ser de números e de procedimento — e dizer isso é parte de ser reprodutível,
não uma ressalva que o enfraquece.
