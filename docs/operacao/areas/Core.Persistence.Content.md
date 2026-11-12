# Transação de conteúdo

> Operacional. 2026-11-04.

- Projeto/pacote: `Core.Persistence.Content`
- Piso: 90 %
- Crítica: sim
- Partes da mutação: 22
- Motivo: Declara o domínio que o banco usa para decidir o que uma consulta de conteúdo vê (decisão
  0006); inteira, a área estoura o teto da 017 (#580) e por isso muta em 22 partes, cada uma em até
  15 min (#557, #716: com 5 partes a 3 passou dos 20 min; #726: com 8, a 2 levou 17 a 20 min por
  17 mutantes de 41 a 61 s cada, que rodam a suíte do banco; #791: com 12, a 6 testou 26
  mutantes de 65 a 93 s cada, a suíte inteira, e passou dos 20 min com 0 mutantes; #801: com 16, a
  3 testou 15 mutantes de 70 a 95 s e estourou, a suíte de 439 testes leva 105 s). A unidade da
  repartição é o trecho de arquivo cortado entre membros, pelo peso (#585, #683), não o arquivo
  nem a contagem de trechos: assim nenhuma parte fica com a maioria dos mutantes. A leitura
  do conteúdo protegido é a área `Core.Persistence.Content.Reading`, a escrita é a
  `Core.Persistence.Content.Writing` e a cifra é a `Core.Persistence.Content.Sealing`.
  Guarda com cada item as ocorrências e o destino de cada uma, seladas com a chave do item, e a
  leitura delas é um evento da cadeia (#84).
  As tabelas `ContentColumns.ReplicationOccurrence` e `ContentColumns.ReplicationMark` (#82,
  decisão 0024) guardam o resumo com chave do texto e do remetente de cada mensagem e a marca
  que aponta para a primeira ocorrência; são só de inserção, valem pelo lote como o conteúdo, e
  a rejeição do lote as remove com ele.
  Na parte 3 da #82, `OccurrencesAsync` e `MarksAsync` leem só o que casa com os resumos do
  lote (`= ANY(@digests)`), nunca o domínio inteiro. Limite conhecido: dois lotes simultâneos
  do mesmo domínio não se veem, porque cada um só enxerga o que o outro já gravou; o mesmo
  texto neles não se marca entre si e a marca chega no próximo lote, sem falha nem duplicata
  (`ON CONFLICT DO NOTHING`). O tamanho gravado, `Length`, e o instante, `SaidAt`, valem em
  pontos de código e em microssegundos.
