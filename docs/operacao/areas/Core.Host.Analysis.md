# Análise do material

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Host.Analysis`
- Piso: 90 %
- Crítica: sim
- Motivo: Leva o texto ao reconhecedor e decide o que a recusa registra e devolve (requisito 021);
  muta contra o projeto próprio, `Core.Host.Analysis.Tests` (#857): contra `Core.Host.Tests`, o
  Stryker gastava uns 7 min de preparo e 13 min ou mais para medir a cobertura dos 1686 testes,
  antes de testar os 13 mutantes, e passou dos 20 min com 0 mutantes; repartir não tira esse custo
  de nenhuma parte. Teste novo de Analysis entra nele; o apoio que o host compartilha fica em
  `Core.Host.Tests`, ligado pelo projeto.
