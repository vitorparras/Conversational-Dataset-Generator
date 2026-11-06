# Lote de ingestão

> Operacional. 2026-11-06.

- Projeto/pacote: `Core.Persistence.Ingestion`
- Piso: 90 %
- Crítica: sim
- Motivo: Abre o lote sem conteúdo e o encerra, aceito ou rejeitado, cada passo com o evento da
  cadeia na mesma transação serializável; a rejeição remove o conteúdo do lote nessa transação, e o
  banco recusa a mudança sem o evento (#74). Decide o que vira acervo e o que é descartado, por isso
  o piso e a mutação dos irmãos de `Core.Persistence.Content`. Sem projeto de teste próprio: roda os
  testes de `Core.Persistence.Content.Tests`
