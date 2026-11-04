# 020 — Telas de exportação e comparação

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Exportação é ato de duas pessoas distintas (requisito 030) e sai sem identidade (requisito 015).
A comparação entre domínios é da fase 10.

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| X1 | Pedir exportação | Curadoria | Domínio, recorte, taxonomia, pisos de suficiência, parecer, condição mais restritiva, deslocamento de datas, manifesto previsto | Submete o pedido | 9 | #626 (regra #153, #154, #158, #162, #167) |
| X2 | Aprovar exportação | Quem responde pelo acervo | O recorte exato e as condições, não o conteúdo item a item | Aprova ou recusa; a mesma pessoa é recusada; recorte alterado invalida | 9 | #627 (regra #156) |
| X3 | Produção | Curadoria | Conferências que bloqueiam: cadeia e selo, auditoria de resíduo, pisos; motivo do bloqueio | Acompanha | 9 | #628 (regra #155, #165) |
| X4 | Entrega | Curadoria | Artefato, marca da entrega, quem recebe | Retira o artefato, registrado como evento | 9 | #629 (regra #157, #164) |
| X5 | Histórico de exportações | Curadoria | Pedidos, aprovações, manifestos, o que ficou retido, entregas | Consulta | 9 | #630 (regra #164, #168) |
| X6 | Comparar domínios | Curadoria | Os dois domínios lado a lado, sem misturar recortes | Consulta | 10 | #180 |

## Observações

- A X2 mostra o que se aprova sem o conteúdo item a item, e nunca o pedido da própria pessoa.
- A X3 mostra o motivo do bloqueio; o bloqueio é regra da custódia (privacidade
  [005](../privacidade/005-verificacao-da-custodia.md) e [008](../privacidade/008-distribuicao.md)).
- A entrega registra a marca no manifesto ([0038](../decisoes/0038-marca-da-entrega-no-manifesto.md)).
- O que ficou retido por esvaziamento aparece no histórico, sem o texto.
- A exportação desloca datas ([0020](../decisoes/0020-exportacao-desloca-datas.md)) e a X1 mostra
  o deslocamento antes do envio.
- A X6 não cria recorte novo: compara os dois, cada um no seu domínio.
- Acessibilidade e teste de navegador do fluxo de aprovação: #591 e #590.
