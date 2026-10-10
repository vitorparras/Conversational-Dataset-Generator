# 0018 — O contrato da fronteira carrega a análise gramatical

> Decisão vigente. 2026-10-07. Substitui a decisão 0011 quanto ao formato da resposta.

## Contexto

Decidir se um nome próprio é pessoa ou lugar depende da função da palavra na frase. Em "falei com
Santos ontem" e "fui para Santos ontem", a palavra é a mesma e o destino dela é oposto: no primeiro
caso é pessoa e vira marcador; no segundo é topônimo e permanece, porque apagar onde algo aconteceu
destrói informação que a análise precisa.

A decisão 0002 escolheu o ecossistema de processamento de linguagem justamente por isso: nele, os
modelos de português entregam o reconhecimento da entidade **junto com** a análise gramatical do
mesmo trecho, num passo só.

A decisão 0011 especificou a resposta do serviço como deslocamento inicial, final, tipo e confiança.
A análise gramatical, que motivou a escolha do ecossistema, não atravessa a fronteira — e o núcleo,
a quem a arquitetura atribui a decisão sobre o destino de cada ocorrência, não recebe com que
decidir. Além disso, a confiança por ocorrência não é produzida no caminho padrão desses modelos: o
campo tenderia a ser preenchido com valor constante, e qualquer regra apoiada nele passaria a não
descartar nada sem falhar.

## Decisão

**A resposta do serviço carrega, para cada ocorrência: deslocamento inicial, deslocamento final,
tipo, classe gramatical e função sintática do núcleo da ocorrência. O campo de confiança sai do
contrato.**

Tudo o mais que a decisão 0011 fixa permanece: deslocamentos em pontos de código com conversão no
núcleo, formato de serialização, transporte restrito à máquina local, distinção entre nada
encontrado e serviço indisponível, e a proibição de deslocamento em registro de log.

## Recusados

**Manter a confiança e acrescentar a análise gramatical.** Preservaria um campo que nenhum documento
usa e que seria preenchido por convenção. Campo que existe sem significado convida a regra que
parece critério e não é.

**Mover a decisão sobre o destino da ocorrência para o serviço.** Resolveria a falta do dado no
núcleo, mas contraria o desenho: a política de que trecho vira o quê é norma, e norma não mora num
serviço que o sistema trata como fonte de observação.

## Consequências

O serviço passa a expor o que o modelo já produz, sem trabalho adicional de inferência.

Se outro motor de reconhecimento vier a ser usado e não oferecer análise gramatical, ele não atende
este contrato — o que é a consequência correta, e não um defeito do contrato.

Nenhum requisito ou política dependia do campo removido, então a troca não altera garantia alguma.

## O que reabre

Se a decisão sobre o destino de cada ocorrência deixar de ser do núcleo, o contrato passa a precisar
de outros campos, e esta decisão perde objeto.

Se surgir necessidade real de descartar ocorrência por medida de incerteza, a confiança volta — mas
acompanhada do mecanismo que de fato a produz, e não como campo a preencher.
