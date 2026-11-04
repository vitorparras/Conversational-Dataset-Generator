# Transação de conteúdo

> Operacional. 2026-11-04.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 3
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); inteira, a área estoura o teto da 017 (#580) e por isso muta em 3 partes, cada uma em até
  12 min (#557). A leitura do conteúdo protegido é a área `Core.Persistence.Content.Reading`, a
  escrita é a `Core.Persistence.Content.Writing` e a cifra é a `Core.Persistence.Content.Sealing`
