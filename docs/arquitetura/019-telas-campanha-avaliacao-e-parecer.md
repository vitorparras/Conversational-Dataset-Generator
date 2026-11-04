# 019 — Telas de campanha, avaliação e parecer

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Campanha de anotação, andamento, concordância e parecer do recorte. Nenhuma tela mostra texto:
a métrica é sem texto (requisito 029) e consultá-la fica registrado como acesso.

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| P1 | Campanha | Curadoria | Regra de dupla anotação, arbitragem, sugestão automática, critério de parada; congelada enquanto roda | Declara e congela | 8 | #621 (regra #136) |
| P2 | Atribuição | Curadoria | Itens por pessoa, sorteio da dupla com semente registrada, reapresentação sem marca visível | Atribui | 8 | #622 (regra #137, #138) |
| M1 | Andamento | Avaliação | Contagens e estado da campanha, sem texto | Consulta | 6 | #623 (regra #118) |
| M1 | Concordância | Avaliação | Total, parcial, principal, dimensões, fronteira; onde a divergência se concentra; "recorte pequeno demais" no lugar do número | Consulta | 8 | #624 (regra #145) |
| M2 | Parecer do recorte | Avaliação | Sim ou não, com a lista do que impediu; o coeficiente só como informação | Consulta | 8 | #625 (regra #152) |

## Observações

- A M1 tem duas telas: o andamento nasce na fase 6 e a concordância, na 8 (requisitos 013 e 016).
- A campanha congelada não se edita: mudar a regra é declarar outra (decisão
  [0017](../decisoes/0017-dupla-anotacao-e-arbitragem.md)).
- Recorte pequeno não vira número: a tela diz que é pequeno demais, no limite da
  [0037](../decisoes/0037-limite-do-recorte-pequeno.md), e nenhum coeficiente o substitui.
- A reapresentação sorteada é igual a um item novo para quem anota; a marca nunca chega à página.
- O parecer é a condição de entrada da exportação: a X1, em
  [020](020-telas-exportacao-e-comparacao.md), mostra o parecer vigente.
- O sorteio guarda a semente para que a atribuição seja recalculável (requisito 016).
- Acessibilidade de tabelas e medidas: #591.
