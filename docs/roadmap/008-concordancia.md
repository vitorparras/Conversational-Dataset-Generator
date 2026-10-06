# Fase 8 — Concordância

> Norma vigente. 2026-10-04.

Medir concordância exige que o mesmo item tenha sido anotado por mais de uma pessoa, e isso não
acontece por acaso: a sobreposição é atribuída de propósito, com proporção declarada. Medir o que
por sorte coincidiu transforma a amostra em amostra de conveniência, e a métrica deixa de dizer o
que parece dizer.

Entram a atribuição com sobreposição, o cálculo por domínio, e a arbitragem que resolve a
divergência. O protocolo de anotação, a definição das medidas e a leitura dos resultados já estão
escritos em `docs/pesquisa/`; esta fase os torna executáveis dentro da ferramenta, que é o que a
decisão [0006](../decisoes/0006-dominios-coexistem.md) exige ao recusar comparação feita à mão.

Aqui existe apenas um domínio anotado. A comparabilidade entre domínios é construída agora e só é
exercida na [fase 10](010-segundo-dominio.md) — e esse intervalo é o risco conhecido desta ordem.

## O que precisa estar pronto antes

A [fase 7](007-anotacao.md), e especificamente as garantias
[010](../requisitos/010-sugestao-nao-e-anotacao.md) e
[011](../requisitos/011-anotacoes-independentes.md). Sem elas a medida roda, devolve número alto e
não autoriza conclusão alguma: mede conformidade com a sugestão ou com o colega, não concordância.

## Critério de término

1. [013](../requisitos/013-concordancia-comparavel.md) é verificado, inclusive a reprodutibilidade
   a partir dos mesmos dados.
2. O cálculo, aplicado a um conjunto sintético com resposta conhecida, devolve o valor conhecido.
3. [012](../requisitos/012-divergencia-arbitrada.md) é verificado: a resolução registra o que foi
   escolhido, por quem e contra o quê.
4. A concordância é calculável também desconsiderando rótulos que vieram de sugestão aceita sem
   alteração, conforme [010](../requisitos/010-sugestao-nao-e-anotacao.md).
5. A proporção de itens com mais de uma anotação é consultável, e resulta de regra declarada.
6. Mudar a taxonomia havendo anotação feita é bloqueado até existir a decisão que a decisão
   [0004](../decisoes/0004-taxonomia-configuravel.md) exige para esse caso.

## O que destrava

A exportação pode declarar a qualidade do que exporta. Dataset anotado sem medida de concordância é
dataset cuja confiabilidade quem recebe não tem como avaliar — e, por isso, não tem como usar.
