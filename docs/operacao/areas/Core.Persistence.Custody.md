# Cadeia de custódia

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Persistence.Custody`
- Piso: 90 %
- Crítica: sim
- Motivo: Dá nome ao evento que encadeia o acervo (autor, item, passo, motivo, resultado e recusas)
  e recusa autor desativado ou inexistente (requisito 019); a transação que o grava é a área
  `Core.Persistence.Custody.Chain`
