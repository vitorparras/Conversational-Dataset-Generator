# 017 — Telas de reversão, esvaziamento e relatos

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Atos que mexem no que o sistema guarda ou revela e que exigem registro: reverter um pseudônimo,
esvaziar um item e decidir um relato de dado pessoal. A reversão é ato de duas pessoas distintas.

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| R1 | Pedir reversão | Curadoria | Pseudônimo nomeado, item, justificativa escrita, o que será feito, por quanto tempo | Submete, um por vez; domínio inteiro é recusado | 5 | #614 (regra #103) |
| R2 | Autorizar reversão | Outra curadoria do domínio | Pedidos pendentes sem identidade, justificativa, prazo | Autoriza ou recusa; pedido próprio é recusado | 5 | #615 (regra #104) |
| R3 | Meus pedidos e entrega | Quem pediu | Estado e validade; a identidade revelada aparece uma vez | Revela | 5 | #616 (regra #105) |
| V1 | Esvaziar item | Função própria | Estado do pedido, motivo, quem pediu e quem executou, marca "esvaziado" | Executa o esvaziamento | 3 | #617 (regra #73) |
| U2 | Relatos de dado pessoal | Curadoria | Item, trecho marcado, justificativa, estado; casos acumulados | Confirma ou rejeita; confirmado, substitui o trecho | 7 | #618 (regra #131) |

## Observações

- A R2 é de outra pessoa com curadoria no mesmo domínio, nunca a de quem pediu (#615); o relato
  é decidido pela curadoria, e a auditoria vê só o registro dele (#618).
- Quem autoriza nunca vê a própria proposta, e o pedido aparece sem a identidade
  (requisito 007, [privacidade 007](../privacidade/007-reversao-da-pseudonimizacao.md)).
- O esvaziamento é da [0028](../decisoes/0028-guarda-descarte-e-esvaziamento.md), de função
  própria: a tela aceita só o identificador do item e não oferece busca (#617).
- O relato nasce na tela de anotação (N2, em [018](018-telas-anotacao-e-arbitragem.md)); a fila
  é a U2, pela [0026](../decisoes/0026-relato-de-dado-pessoal.md).
- O ato de duas pessoas segue o padrão da [0051](../decisoes/0051-navegacao-gestao-e-relatorio.md).
- Acessibilidade e teste de navegador: #591 e #590.
