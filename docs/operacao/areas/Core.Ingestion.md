# Ingestão

> Operacional. 2026-11-06.

- Projeto/pacote: `Core.Ingestion`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 9
- Motivo: Absorve: `Reading`, `Entrance`, `Deciding`, `Review`, `Sanitizing`; confere a forma
  declarada do material contra o conteúdo lido e recusa o lote inteiro quando não se sustenta ou
  quando a leitura é parcial, sem tirar ordem nem autoria do nome do arquivo (decisão 0005); ao
  acomodar o material numa forma que não é a sua, declara por unidade a forma recebida e o que ela
  perde, em código de conjunto fechado (#81). Muta em 9 partes: com 2, depois dos passos de
  normalização (#844), a parte 2 passou dos 20 min com a 1 em 11,7 min; com 3, depois do passo do
  identificador e das propriedades de resíduo (#845), a 1 levou 19 min e a 2 e a 3 passaram dos 20
  min; com 5, depois da leitura por leiaute (#845), a 1, a 3 e a 5 levaram de 12,8 a 16,6 min e a 2
  e a 4 passaram dos 20 min (run 44759379047); com 7, depois da reexecução (#846), a 5 passou dos 20
  min com 213 mutantes, 39 deles de mais de 9 s, quase todos do passo do identificador e esgotados
  (run 44854644709).
