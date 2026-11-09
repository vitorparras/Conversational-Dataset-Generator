# Verificação da cadeia

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Persistence.Custody.Verification`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 2
- Motivo: Recalcula cada elo da cadeia com a própria codificação, numa leitura isolada, e registra o
  veredito como evento do sistema e lê o último que a cadeia registrou (requisito 020)
