# Inclusão do evento na cadeia de custódia

> Operacional. 2026-11-02.

- Projeto/pacote: `Core.Persistence.Custody.Chain.Append`
- Piso: 90 %
- Crítica: sim
- Motivo: Decide, dentro da transação, a ordem entre a mudança e o evento, grava o evento e só
  confirma se ele entrou (requisito 019); separada de `Core.Persistence.Custody.Chain` para que a
  mutação de cada uma passe de metade do tempo do teto da 017. Sem projeto de teste próprio: roda
  os testes de `Core.Persistence.Custody.Chain.Tests`
