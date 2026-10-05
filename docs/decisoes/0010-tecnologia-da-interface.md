# 0010 — A tecnologia da interface

> Decisão vigente. 2026-10-04.

## Contexto

Há duas famílias de tela: a de navegação, gestão e relatório, e a de anotação, onde uma pessoa passa
horas seguidas. É a segunda que decide a tecnologia — e não pelo que ela tem de formulário comum,
que não informa escolha nenhuma, mas pelas exigências que o formulário não alcança.

## Decisão

**As telas são renderizadas no servidor, com troca parcial de região. Sobre elas existe uma camada
de cliente fina, declarada, sem nenhuma regra de decisão.**

A camada faz três coisas e só essas: captura teclado no documento, suprimindo o atalho quando o foco
está em campo de texto; lê a seleção de trecho, que chega do navegador em unidades UTF-16; e mostra
retorno visual antes da confirmação. Não valida rótulo, não deriva categoria, não decide nada. As
posições são gravadas em pontos de código, a unidade do contrato da decisão 0011, e a conversão
acontece no núcleo — só ali.

O atalho de teclado por categoria é a única exigência que não se expressa de forma declarativa, e é
o que obriga a camada a existir: o ganho dele é a mão não sair do teclado a cada decisão, e uma ida
ao servidor por tecla põe o atraso percebido acima do limiar de fração de segundo — o atalho fica
mais lento que o mouse.

O resto fica no servidor por três motivos. Existe estado que não pode atravessar: se houver
reanotação cega para medir consistência, o item precisa parecer normal, e a marca que o identifica,
chegando à página, é descoberta inspecionando-a — a medida mente sem que nada falhe. O controle de
acesso fica com uma fonte só, em vez de uma regra por tela e outra por chamada de dados. E o que
fica fora de tipos, análise estática e cobertura precisa ser pequeno e sem regra de decisão, para
que o pior defeito possível seja retorno visual errado, corrigido na resposta seguinte.
Imperceptível tem de ser o retorno visual, não a gravação: a marca aparece na hora e o servidor
confirma depois. E a troca de região nunca recria o formulário em andamento, senão consultar o
manual de critérios descarta o que não foi submetido.

## Recusados

| Opção | Por quê |
|---|---|
| Aplicação cliente para a tela de anotação | O estado que não pode atravessar passa a atravessar, e o controle de acesso vira duas cópias da mesma regra |
| Renderização no servidor sem camada de cliente nenhuma | Uma ida ao servidor por tecla põe o atraso percebido acima do limiar, e o atalho perde a razão de existir |
| Camada de cliente com regra de decisão | Põe a lógica que produz rótulo fora de tipos, análise estática e cobertura, que é onde a qualidade do dataset é guardada |
| Latência acumulada como critério | Compara-se ao tempo humano de ler e decidir, que é de outra ordem de grandeza: a espera de rede é fração desprezível dele |
| Preferência estética, ou "é o que se usa hoje" | Não sobrevive a alguém perguntando por que a ferramenta foi construída assim |

## Consequências

Comportamento de teclado, troca parcial de região e foco real não se testam sem ferramenta de
navegador: ou ela entra no escopo, ou o atalho fica sem rede de proteção, e isso se declara. E o
teto de tamanho da unidade de anotação vira pré-requisito em aberto — acima de um valor modesto,
lista virtualizada pesa na escolha; abaixo, paginação resolve e o critério some.

Mostrar o que sustentou um palpite automático exige que a procedência seja gravada no momento da
classificação. Se o modelo de dados não guardar isso, a exigência de tela não é implementável.

## O que reabre

Se a camada de cliente precisar de regra de decisão para atender exigência nova, ela deixa de caber
aqui: ou a exigência muda, ou a escolha é refeita por escrito.
