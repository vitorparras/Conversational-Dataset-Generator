# Verificação da cadeia

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Persistence.Custody.Verification`
- Piso: 90 %
- Crítica: sim
- Motivo: Recalcula cada elo da cadeia com a própria codificação, numa leitura isolada, e registra o
  veredito como evento do sistema (requisito 020)
