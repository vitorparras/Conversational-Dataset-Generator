# Parâmetros do evento na cadeia de custódia

> Operacional. 2026-11-02.

- Projeto/pacote: `Core.Persistence.Custody.Chain.Parameters`
- Piso: 90 %
- Crítica: sim
- Motivo: Liga ao comando que grava o evento cada parâmetro por nome e tipo, com os ausentes como
  nulos do banco (requisito 019); separada de `Core.Persistence.Custody.Chain` para que a mutação
  de cada uma passe de metade do tempo do teto da 017. Sem projeto de teste próprio: roda os testes
  de `Core.Persistence.Custody.Chain.Tests`
