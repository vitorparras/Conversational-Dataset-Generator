# 0016 — Mudança de taxonomia com anotação existente

> Decisão vigente. 2026-10-04.

## Contexto

A decisão 0004 faz a taxonomia ser configuração e termina dizendo que mudá-la depois de haver
anotação é mudança de dado, não de configuração — e que isso exige decisão própria sobre o que
acontece com o que já foi anotado. É esta. Sem ela, a primeira definição de categoria que precisar
de ajuste será ajustada sem que ninguém decida o destino das anotações anteriores.

## Decisão

**Anotação é imutável e fica atada à versão da taxonomia sob a qual foi feita.** Mudar a taxonomia
nunca reescreve anotação. O efeito depende do tipo da mudança:

| Mudança na taxonomia | O que acontece com o que já foi anotado |
|---|---|
| Acrescentar categoria | Nada. A categoria nova só existe a partir da versão nova |
| Mudar o rótulo exibido, mantendo o identificador | Nada. O identificador é o que foi gravado |
| Redefinir o que uma categoria inclui | As anotações anteriores deixam de ser comparáveis com as novas, e o material é reanotado para entrar na mesma exportação |
| Remover ou dividir categoria | A anotação anterior não tem equivalente: permanece atada à versão antiga e fica fora de exportação sob a nova |

**Não há migração automática de rótulo.** Converter em lote o que uma pessoa anotou sob uma
definição para o que ela teria anotado sob outra é atribuir-lhe um julgamento que ela não fez. O
rótulo convertido entraria na medida de concordância como se fosse opinião humana, e a medida
passaria a conter decisão da máquina sobre a divergência que ela deveria estar medindo.

## Recusados

| Opção | Por quê |
|---|---|
| Converter em lote por mapa de equivalência | Inventa julgamento humano e contamina a medida de concordância |
| Reanotar todo o acervo a cada mudança | Custo proibitivo: qualquer ajuste de definição descartaria o trabalho acumulado |
| Anotação mutável, sem versão atada | Impossível dizer sob qual definição um rótulo foi dado, e a concordância deixa de significar algo |
| Congelar a taxonomia ao começar a anotar | Transforma definição ruim em defeito permanente do dataset |

## Consequências

A mesma base passa a conter anotações sob versões diferentes da mesma taxonomia. Tudo que lê
anotação — exportação, métrica, tela — precisa dizer de qual versão fala, e o que não disser vai
misturar em silêncio coisas que não se comparam.

Redefinir categoria fica caro de propósito, o que desencoraja ajuste cosmético. O mesmo custo
desencoraja a correção legítima de uma definição ruim, e esse é o lado pior: a saída é a definição
amadurecer antes de haver volume anotado, com anotação de ensaio que ninguém pretende exportar.

Decidir se uma mudança é redefinição ou só esclarecimento de redação passa a ter consequência de
dado, e é julgamento — não há regra automática que o faça.

## O que reabre

Se a reanotação parcial se mostrar inviável no volume previsto, entra conversão declarada, com o
rótulo marcado como convertido e não julgado, e excluído da medida de concordância — por decisão que
substitua esta.
