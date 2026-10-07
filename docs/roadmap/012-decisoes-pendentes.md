# Decisões pendentes

> Norma vigente. 2026-10-04.

Este documento diz **qual fase cada pendência trava** — pendência sem vencimento vira descoberta
tardia, e descoberta tardia aqui significa decidir depois de o código já ter decidido.

Pendência é ponto que uma norma vigente declara em aberto, não assunto que ninguém tratou. Cada uma
abaixo está nomeada no texto que a deixou. A ordem é a de vencimento.

## Abertas

Nenhuma das pendências que travavam fase continua aberta. As quatro que este documento listava
foram decididas, e estão na tabela abaixo.

O que permanece em aberto não é pendência declarada por norma, e sim ambiguidade que a arquitetura
nomeou ao descrever o sistema: o alcance do termo ingestão, qual dos textos intermediários fica em
claro, o piso do escopo do rótulo, o que a revisão humana do lote alcança, e em que condição o texto
bruto deixa de ser recuperável. Nenhuma trava fase; cada uma será decidida por quem implementar, e é
melhor que seja por escrito.

## Fechadas

| Pendência | Decidida por |
|---|---|
| Onde mora o selo externo da cadeia | [0019](../decisoes/0019-selo-externo-da-cadeia.md) |
| Data absoluta ou deslocada no que sai | [0020](../decisoes/0020-exportacao-desloca-datas.md) |
| A quem se comunica um vazamento | [0021](../decisoes/0021-comunicacao-de-incidente.md) |
| As duas licenças | [0009](../decisoes/0009-licenca-do-software-e-dos-dados.md) |
| Formato da fronteira de processo | [0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md) e [0018](../decisoes/0018-contrato-da-fronteira.md) |
| Mudança de taxonomia com anotação existente | [0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md) |
| Chave de cifra por domínio | [0013](../decisoes/0013-chave-de-cifra-por-dominio.md) |

## Fechadas desde que este documento nasceu

| O que estava aberto | Fechada por |
|---|---|
| O formato que atravessa a fronteira de processo | [0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md) |
| O que acontece com a anotação quando a taxonomia muda | [0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md) |
| Chave de cifra por domínio | [0013](../decisoes/0013-chave-de-cifra-por-dominio.md) |

A guarda e a recuperação da chave, que a decisão
[0003](../decisoes/0003-cifra-em-repouso.md) exigia antes de haver dado real, está decidida em
[0014](../decisoes/0014-custodia-da-chave-mestra.md) e detalhada em
[operação](../operacao/). O que a [fase 2](002-custodia-e-cifra.md) ainda deve a ela não é texto: é
o ensaio.

Nenhuma pendência aberta autoriza avançar provisoriamente a fase que ela trava. Decisão tomada
depois do código é decisão tomada pelo código — e o formato das decisões, que exige alternativas
recusadas e condição de reabertura, deixa de ter o que registrar.
