# 030 — O selo na instalação com selo manual

> Operacional. 2026-11-04.

Na instalação que declarou o selo `Manual`, ninguém envia o selo por ela: quem opera o guarda fora
do alcance da base. A verificação que o usa está em
[007](007-verificacao-da-custodia.md). O fluxo é este, com a sessão de quem opera:

1. `GET /custody/seal` emite o selo do topo atual, em texto, para imprimir ou copiar. Não grava
   nada. Guarde o texto onde quem controla a base não alcance.
2. `POST /custody/seal/confirmation`, com `{"seal": "<texto do selo>"}`, grava o evento do selo
   na cadeia, em nome da pessoa da sessão, só se a cadeia tiver aquele evento com o resumo que o
   selo traz. Selo alterado, ou de uma sequência que a cadeia não tem, recebe 409 com o motivo e
   nada é gravado; texto que não é selo recebe 400. Corpo acima de 1 KiB recebe 413.
3. A verificação (`GET` e `POST /custody/verification`) traz o aviso `warning` quando nenhum selo
   foi registrado ou quando o último tem mais de 7 dias. O `/status` traz o mesmo aviso, em
   `sealWarning`, só com sessão de Administração técnica ou Auditoria (sem sessão, só `alive` e
   `ready`), atualizado a cada verificação periódica e logo após cada confirmação; a data do
   selo e o selo ficam atrás da sessão. Repita o fluxo a cada semana: o selo antigo
   cobre a cadeia só até a data dele, e o que entrou depois vale pela coerência dos elos, não pela
   integridade.

Nas instalações `External` e `Internal` essas duas rotas respondem 409: o núcleo envia o selo
sozinho, uma vez por dia, e um selo gravado à mão diria que o destino guarda o que não guarda.

## Como o envio dos outros selos é provado

O certificado recusado é provado com um servidor TLS local e o handler real do envio. O tempo
esgotado não: o limite do pedido é de 30 segundos, fixo em `SealDelivery`, e encurtá-lo para um
teste abriria um gancho de teste na produção. Esse caso fica com o dublê, que lança a exceção.
