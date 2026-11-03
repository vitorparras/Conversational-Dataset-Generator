# Transação de conteúdo

> Operacional. 2026-11-03.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); separada para que a mutação de cada parte fique em até 12 min (#557), dentro do teto da
  017. A leitura do conteúdo protegido é a área `Core.Persistence.Content.Reading`, a escrita é a
  `Core.Persistence.Content.Writing` e a cifra é a `Core.Persistence.Content.Sealing`
