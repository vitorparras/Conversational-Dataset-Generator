# Reconhecedor falso

> Operacional. 2026-11-01.

- Projeto/pacote: `Core.Boundary.Fake`
- Piso: 90 %
- Crítica: sim
- Motivo: Recebe o texto do material e devolve os deslocamentos; determinístico, selecionável por
  `Recognizer:Kind`; recusa texto sem marcador sintético (decisão 0011)
