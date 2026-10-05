# Decisões pendentes

> Norma vigente. 2026-10-04.

Este documento diz **qual fase cada pendência trava** — pendência sem vencimento vira descoberta
tardia, e descoberta tardia aqui significa decidir depois de o código já ter decidido.

Pendência é ponto que uma norma vigente declara em aberto, não assunto que ninguém tratou. Cada uma
abaixo está nomeada no texto que a deixou. A ordem é a de vencimento.

## Abertas

**Onde mora o registro externo do topo da cadeia.** A
[cadeia de custódia](../privacidade/004-cadeia-de-custodia.md) manda registrar o resumo do último
evento fora do banco e fora do alcance de quem o administra, e declara que nenhuma decisão vigente
diz onde. Sem esse lugar a verificação atesta coerência, não integridade: uma base substituída por
outra, coerente consigo mesma, passa sem reclamar. Trava o término da
[fase 2](002-custodia-e-cifra.md), que é quem entrega a verificação da cadeia.

**Data absoluta ou deslocada no que sai.**
[O que se preserva](../privacidade/002-o-que-se-preserva.md) mantém data e hora absolutas no acervo
e deixa pendente o que a exportação leva: a data absoluta, ou um deslocamento uniforme por acervo,
que preserva intervalos e perde a ancoragem. Trava a [fase 9](009-exportacao.md).

**As duas licenças.** A decisão [0009](../decisoes/0009-licenca-do-software-e-dos-dados.md) é ela
própria a pendência, e fixa que nada é publicado antes de as duas existirem. Não trava fase alguma:
publicar não é fase deste roadmap, como [fora de escopo](013-fora-de-escopo.md) registra. Trava a
publicação, que vem depois da [fase 10](010-segundo-dominio.md).

**A quem se comunica um vazamento, e em que prazo.** O
[incidente de vazamento](../operacao/008-incidente-vazamento.md) declara que nenhuma norma vigente
fixa esse dever. Não trava fase: vence no primeiro incidente, e incidente não espera fase.

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
