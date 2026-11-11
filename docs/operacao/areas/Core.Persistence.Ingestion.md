# Lote de ingestão

> Operacional. 2026-11-06.

- Projeto/pacote: `Core.Persistence.Ingestion`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 8
- Motivo: Abre o lote sem conteúdo e o encerra, aceito ou rejeitado, cada passo com o evento da
  cadeia na mesma transação serializável; a rejeição remove o conteúdo do lote nessa transação, e o
  banco recusa a mudança sem o evento (#74). Decide o que vira acervo e o que é descartado, por isso
  o piso e a mutação dos irmãos de `Core.Persistence.Content`. Projeto de teste próprio,
  `Core.Persistence.Ingestion.Tests`, que o mapa da mutação escolhe para a área e roda sozinho.
  Inteira, a área passou dos 20 min com nenhum mutante gerado, duas vezes (#794: jobs 130711713238 e
  130728997549), e por isso muta em 8 partes, no mesmo padrão da `Core.Persistence.Content` (#791),
  cada uma cortada entre membros pelo peso (#585, #683) e com a suíte do banco por mutante.
