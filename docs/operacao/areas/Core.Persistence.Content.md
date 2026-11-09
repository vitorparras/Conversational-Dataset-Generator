# Transação de conteúdo

> Operacional. 2026-11-04.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 8
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); inteira, a área estoura o teto da 017 (#580) e por isso muta em 8 partes, cada uma em até
  15 min (#557, #716: com 5 partes a 3 passou dos 20 min). A unidade da repartição é o trecho
  de arquivo cortado entre membros, pelo peso (#585, #683), não o arquivo nem a contagem de
  trechos: assim nenhuma parte fica com a maioria dos mutantes. A leitura do conteúdo protegido é
  a área `Core.Persistence.Content.Reading`, a escrita é a
  `Core.Persistence.Content.Writing` e a cifra é a `Core.Persistence.Content.Sealing`.
  Guarda com cada item as ocorrências e o destino de cada uma, seladas com a chave do item, e a
  leitura delas é um evento da cadeia (#84)
