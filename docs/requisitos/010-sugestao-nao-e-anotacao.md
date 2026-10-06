# 010 — Sugestão automática não é anotação

> Norma vigente. 2026-10-04.

**O que a máquina sugere só se torna decisão de pessoa por um ato dela, e a procedência de cada
rótulo é derivada pelo sistema, nunca declarada por quem anota nem pela tela.**

## Por que existe

A anotação deste sistema é assistida, e assistência acelera e enviesa pelo mesmo mecanismo: diante
de uma sugestão plausível, concordar custa menos que examinar. Se sugestão e decisão se confundem na
gravação, a concordância entre pessoas deixa de medir pessoas e passa a medir quanto cada uma
aceitou a máquina — e o número sobe justamente quando a qualidade cai.

Daí decorrem duas exigências que parecem detalhe de tela e não são. A procedência tem de sair da
comparação entre o que foi servido e o que foi submetido: declarada, ela registra o que a pessoa diz
ter feito, e a taxa de aceitação — o número que este requisito existe para tornar visível — passa a
ser auto-reportada. E a sugestão é exibida, não pré-selecionada: marcada de antemão, submeter sem
tocar em nada registra aceitação corretamente e zera o custo de aceitar, produzindo exatamente o
viés que a medida deveria detectar.

A [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) trata o dataset como contribuição
própria. Um dataset cuja anotação não se sabe de onde veio não serve de referência para ninguém.

## Critérios de aceitação

- Dado um formulário de anotação com sugestão disponível, quando ele é apresentado, então nenhuma
  categoria vem pré-selecionada, e submeter sem escolher não grava rótulo algum.
- Dado um rótulo submetido, quando sua procedência é determinada, então ela resulta da comparação
  entre a sugestão servida naquele carregamento e o que foi submetido.
- Dado um rótulo gravado, quando se pergunta sua origem, então a resposta distingue sugestão aceita
  sem alteração, sugestão corrigida e rótulo criado pela pessoa.
- Dado um conjunto de itens, quando a concordância é calculada, então ela é calculável também
  desconsiderando os rótulos que vieram de sugestão aceita sem alteração.
- Dado que o mecanismo de sugestão mude, quando ele muda, então as anotações já gravadas permanecem
  como estavam, e as que foram sugeridas por ele continuam identificáveis.

## O que não exige

Não exige recusar a assistência, que existe porque anotar à mão não escala. Não exige que a taxa de
aceitação seja baixa — exige que seja visível, para que quem avalia o dataset decida o que fazer com
ela. Não exige guardar para sempre toda sugestão servida: basta que ela esteja disponível quando a
submissão é comparada, e o que a pessoa ignorou por completo não precisa ficar registrado.
