# 001 — Protocolo de anotação

> Operacional. 2026-10-04.

Uma campanha de anotação é declarada antes de começar e não muda no meio. Ela declara: domínio,
versão da taxonomia, forma da unidade de análise, conjunto de itens, quem anota, quantas pessoas por
item, regra de arbitragem, se há sugestão automática na tela, e critério de parada. Mudar qualquer
um desses abre campanha nova — concordância medida sobre dois protocolos misturados devolve um
número que não descreve nenhum dos dois.

## Quem anota

Pessoas treinadas na taxonomia daquele domínio e autorizadas naquele domínio. Treinar é ler as
definições e anotar um conjunto de calibração cujas respostas esperadas já existem; qualificar é
atingir, nesse conjunto, o piso que a campanha declarou. Anotação de pessoa não qualificada não
entra na medida — e, se entrou, a medida é refeita sem ela, declarando que foi refeita.

## Quantas pessoas por item

Duas, na fração declarada do material; o resto é anotado uma vez. A
[decisão 0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md) fixa esse desenho e delega a esta
pasta os dois números que o completam — **a fração em dupla anotação e o piso de concordância
aceitável**. Enquanto não houver campanha, nenhum dos dois tem valor atribuído: cada campanha
declara o seu, e a fração só muda por escrito.

A fração é sorteada do mesmo conjunto, ao acaso. A concordância é medida nela e estendida ao resto,
e a extensão não vale se a parte tiver sido escolhida por conveniência: itens fáceis, ou os do
começo, devolvem concordância que não descreve o dataset. A proporção é publicada junto do número.

Uma fração declarada dos itens volta à mesma pessoa depois de um intervalo. A concordância dela
consigo mesma é o teto do que se pode esperar entre pessoas diferentes.

## O que o anotador vê

O texto sanitizado, a estrutura que a forma exige — autoria por mensagem e ordem, numa conversa —,
as definições da taxonomia com os exemplos do que inclui e do que não inclui, e a opção de marcar o
item como não decidível, com motivo. Sem essa opção explícita o anotador chuta, e o chute entra no
dataset como decisão.

Os itens chegam em ordem sorteada por pessoa, para que a queda de atenção no fim da sessão não
recaia nos mesmos itens para todo mundo.

## O que o anotador não vê

O mapa de reversão e qualquer identidade real; o texto bruto anterior à sanitização; material de
outro domínio; a anotação de quem mais anotou o item; a arbitragem. E **que aquele item seja de
dupla anotação**: saber muda o cuidado, e o que se mediria então seria o cuidado, não a clareza da
taxonomia. Que uma anotação não alcance a outra é garantia do sistema e não regra de conduta, pelo
requisito de
[anotações independentes](../requisitos/011-anotacoes-independentes.md). Ao protocolo cabe não
desfazer isso por fora: nada de alinhar o caso em reunião, ou revisar a dois, antes de as duas
anotações estarem fechadas.

Se a campanha mostra sugestão automática, ela declara isso, e cada rótulo registra como foi
produzido, pelo requisito de
[sugestão não é anotação](../requisitos/010-sugestao-nao-e-anotacao.md). Para a medida, o que
importa é que concordância obtida com sugestão na tela não se compara com a obtida sem — são tarefas
diferentes, e o número não avisa.
