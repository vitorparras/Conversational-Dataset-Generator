# Decisões pendentes

> Norma vigente. 2026-10-04.

Este documento diz **qual fase cada pendência trava** — pendência sem vencimento vira descoberta
tardia, e descoberta tardia aqui significa decidir depois de o código já ter decidido.

Pendência é ponto que uma norma vigente declara em aberto, não assunto que ninguém tratou. Cada uma
abaixo está nomeada no texto que a deixou. A ordem é a de vencimento.

## Abertas

**Nenhuma.** As pendências que travavam fase foram decididas, e as cinco ambiguidades que a
arquitetura nomeou ao descrever o sistema também — estão nas tabelas abaixo.

Pendência nova se registra aqui quando a norma a declara em aberto, não quando alguém tropeça nela.
Esta seção vazia é o estado correto, e não dispensa a conferência: duas ambiguidades ficaram fora
desta lista enquanto estavam abertas, e o que as revelou foi alguém lendo a arquitetura para
implementar.

## Fechadas

| Pendência | Decidida por |
|---|---|
| Onde mora o selo externo da cadeia | [0019](../decisoes/0019-selo-externo-da-cadeia.md) |
| Data absoluta ou deslocada no que sai | [0020](../decisoes/0020-exportacao-desloca-datas.md) |
| A quem se comunica um vazamento | [0021](../decisoes/0021-comunicacao-de-incidente.md) |
| As duas licenças | [0009](../decisoes/0009-licenca-do-software-e-dos-dados.md) |
| O formato que atravessa a fronteira de processo | [0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md), [0018](../decisoes/0018-contrato-da-fronteira.md) |
| O que acontece com a anotação quando a taxonomia muda | [0016](../decisoes/0016-mudanca-de-taxonomia-com-anotacao-existente.md) |
| Chave de cifra por domínio | [0013](../decisoes/0013-chave-de-cifra-por-dominio.md) |
| A guarda e a recuperação da chave-mestra | [0014](../decisoes/0014-custodia-da-chave-mestra.md) |

## As cinco ambiguidades da arquitetura

| O que estava ambíguo | Resolvida por |
|---|---|
| O alcance do termo ingestão | [dado pessoal](../privacidade/001-dado-pessoal.md) |
| Qual dos textos intermediários fica em claro | [0030](../decisoes/0030-qual-texto-fica-em-claro.md) |
| O piso do escopo do rótulo | [requisito 006](../requisitos/006-pseudonimizacao-preserva-contexto.md) |
| O que a revisão humana do lote alcança | [requisito 035](../requisitos/035-revisao-do-lote-por-amostra.md) |
| Até quando o texto bruto é recuperável | [0034](../decisoes/0034-bruto-sem-prazo-proprio.md) |

O que a [fase 2](002-custodia-e-cifra.md) ainda deve à custódia da chave não é texto: é o ensaio.

Nenhuma pendência aberta autoriza avançar provisoriamente a fase que ela trava. Decisão tomada
depois do código é decisão tomada pelo código — e o formato das decisões, que exige alternativas
recusadas e condição de reabertura, deixa de ter o que registrar.
