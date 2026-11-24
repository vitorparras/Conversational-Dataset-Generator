# Eventos da reversão na cadeia

> Norma pendente. 2026-11-22. O que cada evento das fatias C e D aponta, antes do plano da fatia.

O evento de custódia não tem texto livre: quem é o autor, quando é o instante do evento, e o resto
é o tipo e o id do item, o passo, o motivo e uma `reference` de até 64 caracteres na forma
`^[A-Za-z0-9._/-]{1,64}$`
(`core/src/Core.Persistence/Migrations/Sql/202611201200-sanitization-rerun.sql`, linha 129). A
lista de tipos de item é fechada (mesma migração, linhas 103 a 105). As fatias C e D acrescentam
três tipos: reversal_request, reversal_authorization e reversal_revelation.

O marcador cabe na `reference` e não é dado pessoal: está no texto que a anotação lê. O porquê e o
uso do pedido nunca entram no evento; ficam selados na linha do pedido
([modelo de dados](modelo-de-dados.md), fatia C).

| Passo | Tipo do item | Id do item | `reference` | Motivos |
|---|---|---|---|---|
| reversal_requested | reversal_request | o pedido | o marcador | administrative_act |
| reversal_request_refused, escopo de um item | item_text | o item pedido | o marcador | reversal_function |
| reversal_request_refused, escopo amplo | domain | o domínio ativo | dominio, varios-itens ou varios-marcadores | reversal_scope |
| reversal_request_refused, marcador fora do item | item_text | o item pedido | o marcador | reversal_scope |
| reversal_request_read | reversal_request | o pedido | — | administrative_act |
| reversal_authorized | reversal_authorization | a autorização | o id do pedido | administrative_act |
| reversal_authorization_refused | reversal_request | o pedido | — | reversal_self, reversal_function |
| reversal_declined | reversal_request | o pedido | — | administrative_act |
| reversal_revealed | reversal_revelation | a revelação | o id da autorização | administrative_act |
| reversal_denied | reversal_authorization | a autorização | — | reversal_expired, reversal_used, reversal_emptied |

Quem pediu, quem autorizou e quando se leem juntando o evento à linha que ele aponta: o pedido
guarda quem pediu e o escopo; a autorização, quem autorizou e até quando; a revelação, a
autorização. A recusa de pedido não cria linha (#103 CA-1, "nenhum pedido pendente é criado"): o
escopo dela está no próprio evento, pelo item e pelo marcador, ou pela classe do escopo amplo.

Pedido incompleto (#103 CA-1) não grava evento: volta ao formulário com os erros. Com a cadeia
quebrada, nenhum destes eventos é gravado ([riscos](riscos.md), cadeia quebrada).

## Testes de banco

Na fatia C, `Core.Persistence.Content.Tests`: EachReversalStepPointsToItsListedKindAndReference
(um caso por linha da tabela até reversal_declined) e RefusalReferenceCarriesNoText (a forma da
`reference` recusa espaço e acento). Na fatia D, o mesmo teste ganha as linhas de reversal_revealed
e reversal_denied.

Ver [contratos](contratos.md) e [modelo de dados](modelo-de-dados.md).
